using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using ClassicUs.Reactor;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Reactor 1.2 exposes only 39 custom RPC ids. Town Of Us has more role
    /// messages than that, so registering every message directly leaves roles
    /// near the end of the allocator without networking. This transport uses a
    /// single Reactor RPC and dispatches its named payload to the existing
    /// role handler, keeping the handlers' host-side validation unchanged.
    /// </summary>
    internal static class TownOfUsRpcMux
    {
        private const string TransportKey = "townofus.RpcMux";
        private static readonly Dictionary<string, MethodInfo> Handlers = new();
        private static readonly Dictionary<string, int> _handlerReceiveCounts = new();
        private static readonly Dictionary<string, float> _handlerLastReceiveTime = new();
        internal static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("TownOfUs RPC");
        private static bool _installed;
        internal static bool _warnedMuxDown;

        /// <summary>
        /// True once the mux transport is installed. When false (install failed
        /// or config disabled), RpcRegistration skips role RPC keys entirely so
        /// they never overflow Reactor's finite native RPC-id range.
        /// </summary>
        internal static bool Active => _installed;

        internal static void Install()
        {
            if (_installed) return;
            var harmony = new Harmony(TownOfUsPlugin.Guid + ".rpcmux");
            try
            {
                harmony.CreateClassProcessor(typeof(ReactorAPI_RegisterRpcMethods_MuxPatch)).Patch();
                harmony.CreateClassProcessor(typeof(ReactorAPI_SendRpcMethod_MuxPatch)).Patch();

                // This type is deliberately excluded by the registration prefix so
                // Reactor reserves exactly one real transport id for the mod.
                ReactorAPI.RegisterRpcMethods(typeof(TownOfUsRpcMux));
            }
            catch
            {
                // All-or-nothing: a half-installed mux (registration prefix live,
                // send prefix missing) would reserve no mod ids while every role
                // send still went through Reactor's raw path -> GetId returns 0
                // -> callId-0 messages -> the exact segfault we are fixing.
                harmony.UnpatchSelf();
                throw;
            }
            _installed = true;
        }

        internal static bool Register(Type type)
        {
            // Only multiplex this mod's own types. Other plugins (the ManuAPI
            // base, Reactor itself, or third-party mods) register through the
            // same static method; returning false for them would steal their
            // RPC ids and break their networking.
            if (type == null || type == typeof(TownOfUsRpcMux)) return true;
            if (type.Assembly != typeof(TownOfUsRpcMux).Assembly) return true;
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                string key = null;
                foreach (var attribute in method.GetCustomAttributesData())
                {
                    if (attribute.AttributeType != typeof(ReactorRpcAttribute) || attribute.ConstructorArguments.Count != 1) continue;
                    key = attribute.ConstructorArguments[0].Value as string;
                    break;
                }
                if (string.IsNullOrEmpty(key)) continue;
                Handlers[key] = method;
            }
            return false;
        }

        /// <summary>
        /// Plugin-facing send entry point used by every role system in place of
        /// ReactorAPI.SendRpcMethod. Safe in every state:
        ///   * mux active  -> routes through the single transport (or passes
        ///     foreign classicus.* keys through to their own registrations);
        ///   * mux down    -> townofus.* keys are dropped so they can never
        ///     resolve to call id 0 (a native game RPC) and corrupt the session.
        /// </summary>
        internal static void Send(string key, params object[] args)
        {
            // TrySend returns true when the original ReactorAPI call should run
            // (transport key / foreign key) and false when it was handled or
            // dropped (multiplexed role keys / unreserved townofus keys).
            if (TrySend(key, args)) ReactorAPI.SendRpcMethod(key, args);
        }

        internal static bool TrySend(string key, object[] args)
        {
            if (key == TransportKey) return true; // the transport itself: run Reactor's real send
            if (!Handlers.ContainsKey(key))
            {
                // Foreign keys (classicus.*, third-party mods) pass through to
                // their own Reactor registrations. But a mod key that was never
                // captured (disabled role, unregistered system) must NOT reach
                // GetId() -> 0 -> callId-0 segfault: drop it instead.
                if (key.StartsWith("townofus.", StringComparison.Ordinal)) return false;
                return true;
            }
            try
            {
                // Reactor 1.1 only supports bool/byte/int/float/string RPC arguments,
                // so the binary payload crosses the transport as a base64 string.
                ReactorAPI.SendRpcMethod(TransportKey, key, Convert.ToBase64String(Serialize(args)));
            }
            catch (Exception e)
            {
                Log.LogError("Could not send " + key + ": " + e.Message);
            }
            return false;
        }

        [ReactorRpc(TransportKey)]
        private static void Receive(byte senderId, string key, string payload)
        {
            if (!Handlers.TryGetValue(key, out var method))
            {
                Log.LogWarning("Received unknown RPC: " + key);
                return;
            }
            try
            {
                var parameters = method.GetParameters();
                var values = new object[parameters.Length];
                values[0] = senderId;
                var bytes = string.IsNullOrEmpty(payload) ? Array.Empty<byte>() : Convert.FromBase64String(payload);
                using var stream = new MemoryStream(bytes, false);
                using var reader = new BinaryReader(stream);
                for (var i = 1; i < parameters.Length; i++) values[i] = Read(reader, parameters[i].ParameterType);
                method.Invoke(null, values);

                // Track for diagnostics.
                if (!_handlerReceiveCounts.TryGetValue(key, out var count)) count = 0;
                _handlerReceiveCounts[key] = count + 1;
                _handlerLastReceiveTime[key] = Time.time;
            }
            catch (Exception e)
            {
                Log.LogError("Could not dispatch " + key + ": " + e);
            }
        }

        /// <summary>
        /// Clears per-handler statistics at game end. Call from GameEvents.GameEnded
        /// so the next match's diagnostics start fresh.
        /// </summary>
        internal static void ResetStats()
        {
            _handlerReceiveCounts.Clear();
            _handlerLastReceiveTime.Clear();
        }

        /// <summary>
        /// Per-handler receive count, for debugging. Returns 0 for unknown keys.
        /// </summary>
        internal static int ReceiveCount(string key) =>
            _handlerReceiveCounts.TryGetValue(key, out var c) ? c : 0;

        private static byte[] Serialize(object[] values)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                foreach (var value in values ?? Array.Empty<object>()) Write(writer, value);
            }
            return stream.ToArray();
        }

        private static void Write(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case bool v: writer.Write(v); break;
                case byte v: writer.Write(v); break;
                case sbyte v: writer.Write(v); break;
                case short v: writer.Write(v); break;
                case ushort v: writer.Write(v); break;
                case int v: writer.Write(v); break;
                case uint v: writer.Write(v); break;
                case long v: writer.Write(v); break;
                case ulong v: writer.Write(v); break;
                case float v: writer.Write(v); break;
                case double v: writer.Write(v); break;
                case string v: writer.Write(v ?? string.Empty); break;
                case byte[] v: writer.Write(v?.Length ?? -1); if (v != null) writer.Write(v); break;
                case Vector2 v: writer.Write(v.x); writer.Write(v.y); break;
                case Vector3 v: writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); break;
                default: throw new NotSupportedException("Unsupported RPC argument: " + value?.GetType());
            }
        }

        private static object Read(BinaryReader reader, Type type)
        {
            if (type == typeof(bool)) return reader.ReadBoolean();
            if (type == typeof(byte)) return reader.ReadByte();
            if (type == typeof(sbyte)) return reader.ReadSByte();
            if (type == typeof(short)) return reader.ReadInt16();
            if (type == typeof(ushort)) return reader.ReadUInt16();
            if (type == typeof(int)) return reader.ReadInt32();
            if (type == typeof(uint)) return reader.ReadUInt32();
            if (type == typeof(long)) return reader.ReadInt64();
            if (type == typeof(ulong)) return reader.ReadUInt64();
            if (type == typeof(float)) return reader.ReadSingle();
            if (type == typeof(double)) return reader.ReadDouble();
            if (type == typeof(string)) return reader.ReadString();
            if (type == typeof(byte[])) { var count = reader.ReadInt32(); return count < 0 ? null : reader.ReadBytes(count); }
            if (type == typeof(Vector2)) return new Vector2(reader.ReadSingle(), reader.ReadSingle());
            if (type == typeof(Vector3)) return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            throw new NotSupportedException("Unsupported RPC parameter: " + type);
        }
    }

    /// <summary>
    /// Registration gate used by TownOfUsPlugin for every role type. Runs the
    /// real Reactor registration only while the mux transport is live; when the
    /// mux is down, role keys are skipped so the finite native RPC-id range
    /// (212-250) can never overflow into call id 0.
    /// </summary>
    internal static class RpcRegistration
    {
        public static void Register(Type type)
        {
            if (type == null) return;
            if (!TownOfUsRpcMux.Active)
            {
                // Log once: a down mux degrades every role to local-only, so one
                // prominent warning beats 27 repeated ones at startup.
                if (!TownOfUsRpcMux._warnedMuxDown)
                {
                    TownOfUsRpcMux._warnedMuxDown = true;
                    TownOfUsRpcMux.Log.LogWarning(
                        "RPC mux inactive - role RPC handlers stay local-only (no cross-client role state).");
                }
                return;
            }
            ReactorAPI.RegisterRpcMethods(type);
        }
    }

    [HarmonyPatch(typeof(ReactorAPI), nameof(ReactorAPI.RegisterRpcMethods), new[] { typeof(Type) })]
    internal static class ReactorAPI_RegisterRpcMethods_MuxPatch
    {
        private static bool Prefix(Type type) => TownOfUsRpcMux.Register(type);
    }

    [HarmonyPatch(typeof(ReactorAPI), nameof(ReactorAPI.SendRpcMethod), new[] { typeof(string), typeof(object[]) })]
    internal static class ReactorAPI_SendRpcMethod_MuxPatch
    {
        private static bool Prefix(string key, object[] args) => TownOfUsRpcMux.TrySend(key, args);
    }
}
