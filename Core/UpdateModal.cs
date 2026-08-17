using System;
using ClassicUs.ManuAPI;
using TMPro;
using UnityEngine;
using TownOfUs.ManuAPI.Core;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// In-game "Update available" modal: a dark full-screen backdrop, a title
    /// and message, and Update / Later buttons.
    ///
    /// Built entirely from primitives already proven in this stack:
    ///  - backdrop uses HudManager.FullScreen's sprite (fullscreen white quad),
    ///  - text is a fresh TextMeshPro copying font/material from an existing TMP
    ///    (same approach as ManuAPI's ModBadgeAPI),
    ///  - buttons are clones of the native KillButton stripped down to
    ///    PassiveButton + TMP + AspectPosition (same as ManuAPI's AbilityButton).
    ///
    /// Download completion is polled from the HudManager.Update patch (no
    /// uncertain Unity threading helpers), and everything is defensive: any
    /// exception hides the modal and logs, never crashes the game.
    /// </summary>
    internal static class UpdateModal
    {
        private static GameObject _root;
        private static TextMeshPro _titleText;
        private static TextMeshPro _messageText;
        private static TextMeshPro _textSource;
        private static GameObject _updateButton;
        private static GameObject _laterButton;
        private static System.Threading.Tasks.Task<string> _downloadTask;

        public static bool IsVisible => _root != null && _root && _root.activeSelf;

        /// <summary>Called from the HudManager.Update patch each frame.</summary>
        public static void Poll()
        {
            try
            {
                // Show the prompt when the async check found a newer version.
                if (!IsVisible && UpdateSystem.ShouldPromptNow())
                {
                    Show(UpdateSystem.Latest);
                    return;
                }

                // Advance the download state machine.
                if (_downloadTask != null && IsVisible)
                {
                    if (_downloadTask.IsCompleted)
                    {
                        string status;
                        if (_downloadTask.IsFaulted || _downloadTask.IsCanceled)
                            status = "Update failed: " + (_downloadTask.Exception?.GetBaseException()?.Message ?? "unknown error");
                        else
                            status = _downloadTask.Result;
                        _downloadTask = null;
                        _messageText.text = status;
                        if (_laterButton != null) _laterButton.SetActive(true);
                    }
                }
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Update modal poll: " + e.Message);
            }
        }

        public static void Show(UpdateSystem.UpdateInfo info)
        {
            try
            {
                if (!HudManager.InstanceExists) return;
                var hud = HudManager.Instance;

                EnsureCreated(hud);
                if (_root == null) return;

                _titleText.text = "Update Available";
                _messageText.text = BuildMessage(info);
                if (_updateButton != null)
                    _updateButton.SetActive(UpdateConfig.AllowDownload?.Value != false);
                if (_laterButton != null) _laterButton.SetActive(true);
                _root.SetActive(true);
                UpdateSystem.MarkPromptShown();
            }
            catch (Exception e)
            {
                BepInEx.Logging.Logger.CreateLogSource("TownOfUs").LogError("Update modal: " + e.Message);
                Hide();
            }
        }

        public static void Hide()
        {
            try
            {
                if (_root != null && _root.activeSelf) _root.SetActive(false);
            }
            catch { }
        }

        private static string BuildMessage(UpdateSystem.UpdateInfo info)
        {
            string msg = "Town Of Us v" + info.Version + " is available (you have v" +
                         UpdateSystem.CurrentVersion + ").\n\n";
            if (!string.IsNullOrEmpty(info.Notes))
                msg += info.Notes + "\n\n";
            msg += UpdateConfig.AllowDownload?.Value == false
                ? "Downloads are disabled in the config — download the new build manually."
                : "Download and install it now?";
            return msg;
        }

        private static void EnsureCreated(HudManager hud)
        {
            if (_root != null && _root) return;

            _root = new GameObject("ToU_UpdateModal");
            _root.transform.SetParent(hud.transform, false);

            // Fullscreen dark backdrop using the game's own fullscreen sprite.
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(_root.transform, false);
            var renderer = backdrop.AddComponent<SpriteRenderer>();
            if (hud.FullScreen != null) renderer.sprite = hud.FullScreen.sprite;
            renderer.color = new Color(0f, 0f, 0f, 0.82f);
            renderer.sortingOrder = 100;

            // Title + message text (font borrowed from an existing HUD label).
            // The source component is kept so text can be cloned instead of
            // AddComponent'd — see CreateText.
            _textSource = hud.GameSettingsTMP;
            var font = hud.GameSettingsTMP != null ? hud.GameSettingsTMP.font : null;
            var material = hud.GameSettingsTMP != null ? hud.GameSettingsTMP.fontSharedMaterial : null;

            _titleText = CreateText("Title", font, material, new Vector3(0f, 1.3f, 0f), 4.2f, 101);
            _titleText.alignment = TextAlignmentOptions.Center;

            _messageText = CreateText("Message", font, material, new Vector3(0f, 0.35f, 0f), 2.2f, 102);
            _messageText.alignment = TextAlignmentOptions.Center;
            _messageText.enableWordWrapping = true;

            // Buttons cloned from the native KillButton (AbilityButton pattern).
            _updateButton = CreateButton(hud, "Update", "Update", new Vector3(-0.9f, -1.1f, 0f), OnUpdateClicked);
            _laterButton = CreateButton(hud, "Later", "Later", new Vector3(0.9f, -1.1f, 0f), OnLaterClicked);
        }

        private static TextMeshPro CreateText(string name, TMP_FontAsset font, Material material, Vector3 localPos, float fontSize, int sortOrder)
        {
            GameObject go;
            TextMeshPro tmp;

            // Clone the game's own working world-space TMP rather than
            // AddComponent<TextMeshPro>. A freshly AddComponent'd TMP keeps its
            // native default font size (36) and ignores the fontSize set before
            // it is awake, rendering the text as giant glyphs. A clone arrives
            // fully initialized (font, mesh, material, awake state) so fontSize
            // applies normally (same fix as GameConfigOverlay.MakeText).
            if (_textSource != null)
            {
                go = UnityEngine.Object.Instantiate(_textSource.gameObject, _root.transform);
                go.name = name;
                go.transform.localPosition = localPos;
                go.transform.localScale = Vector3.one;
                go.transform.localRotation = Quaternion.identity;
                // Text labels must never intercept clicks: defensively drop any
                // collider the clone inherited (PassiveButtonManager routes
                // clicks by collider).
                foreach (var col in go.GetComponentsInChildren<Collider2D>(true))
                    if (col != null) UnityEngine.Object.Destroy(col);
                tmp = go.GetComponent<TextMeshPro>();
                if (tmp == null) tmp = go.GetComponentInChildren<TextMeshPro>(true);
                if (tmp == null)
                {
                    UnityEngine.Object.Destroy(go);
                    return null;
                }
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(_root.transform, false);
                go.transform.localPosition = localPos;
                go.transform.localScale = Vector3.one;
                tmp = go.AddComponent<TextMeshPro>();
                if (font != null) tmp.font = font;
                if (material != null) tmp.fontSharedMaterial = material;
            }

            tmp.text = "";
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.sortingOrder = sortOrder;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static GameObject CreateButton(HudManager hud, string id, string label, Vector3 localPos, Action onClick)
        {
            if (hud.KillButton == null) return null;

            var clone = UnityEngine.Object.Instantiate(hud.KillButton.gameObject, _root.transform);
            clone.name = "ToU_Button_" + id;

            // Strip everything except what we need (AbilityButton pattern).
            foreach (var comp in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (comp == null) continue;
                if (comp.TryCast<PassiveButton>() != null) continue;
                if (comp.TryCast<TextMeshPro>() != null) continue;
                if (comp.TryCast<AspectPosition>() != null) continue;
                comp.enabled = false;
                UnityEngine.Object.Destroy(comp);
            }

            // Center-screen placement.
            var aspect = clone.GetComponent<AspectPosition>();
            if (aspect == null) aspect = clone.AddComponent<AspectPosition>();
            aspect.parentCam = hud.UICamera;
            aspect.Alignment = AspectPosition.EdgeAlignments.Center;
            aspect.DistanceFromEdge = localPos;
            aspect.updateAlways = true;
            aspect.AdjustPosition();

            // Label.
            var tmp = clone.GetComponentInChildren<TextMeshPro>();
            if (tmp != null)
            {
                tmp.text = label;
                tmp.fontSize = Mathf.Max(2.5f, tmp.fontSize);
            }

            // Delegate-free click dispatch (see ClickRouter): the native
            // pipeline calls PassiveButton.ReceiveClickDown for every collider
            // under the mouse, so name this button's PassiveButton GameObject
            // with the unique id and let the ClickRouter prefix route the click.
            // OnClick is deliberately left untouched — marshalling a managed
            // UnityAction via AddListener triggers the game's protection.
            var passive = clone.GetComponentInChildren<PassiveButton>();
            if (passive != null && onClick != null)
            {
                passive.gameObject.name = clone.name;
                ClickRouter.Register(clone.name, onClick);
            }

            clone.SetActive(true);
            return clone;
        }

        private static void OnUpdateClicked()
        {
            if (_downloadTask != null) return;
            if (UpdateSystem.Latest == null) return;
            if (UpdateConfig.AllowDownload?.Value == false) return;

            try
            {
                if (_updateButton != null) _updateButton.SetActive(false);
                if (_laterButton != null) _laterButton.SetActive(false);
                _messageText.text = "Downloading update…";

                // Started on a background thread; completion is polled in Poll().
                _downloadTask = System.Threading.Tasks.Task.Run(() => UpdateSystem.DownloadAndStageAsync());
            }
            catch (Exception e)
            {
                _messageText.text = "Update failed: " + e.Message;
                if (_laterButton != null) _laterButton.SetActive(true);
            }
        }

        private static void OnLaterClicked()
        {
            Hide();
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    internal static class HudManager_Update_UpdateModalPatch
    {
        private static void Postfix()
        {
            UpdateModal.Poll();
        }
    }
}
