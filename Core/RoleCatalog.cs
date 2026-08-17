using UnityEngine;

namespace TownOfUs.ManuAPI.Core
{
    /// <summary>
    /// Single source of truth for every custom role's presentation metadata:
    /// the under-name display name, its color, the flavor description, and the
    /// task-stats text shown on the tasks tab ("Your Role: X — ...").
    ///
    /// RolePresentation (name tags) and RoleInfoCard (tasks-tab card) both read
    /// from this table, so changing a role's name, color, description, or task
    /// text is a one-line edit here.
    /// </summary>
    internal readonly struct RoleDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly RoleTeamTypes Team;
        public readonly Color Color;
        public readonly string Description;
        public readonly string TaskText;

        public RoleDef(string id, string name, RoleTeamTypes team, Color color, string description, string taskText)
        {
            Id = id;
            Name = name;
            Team = team;
            Color = color;
            Description = description;
            TaskText = taskText;
        }
    }

    internal static class RoleCatalog
    {
        public static readonly RoleDef[] All =
        {
            // ---- Crewmate ----
            new("townofus.Sheriff",      "Sheriff",      RoleTeamTypes.Crewmate,
                new Color(0.95f, 0.8f, 0.2f, 1f),   "Shoot the impostors",
                "Shoot Impostors. Shooting a non-enemy may kill you."),
            new("townofus.Engineer",     "Engineer",     RoleTeamTypes.Crewmate,
                new Color(0.2f, 0.85f, 0.95f, 1f),   "Use vents to move around the map.",
                "Fix sabotages from anywhere and use vents."),
            new("townofus.Medic",        "Medic",        RoleTeamTypes.Crewmate,
                new Color(0.3f, 0.95f, 0.55f, 1f),   "Protect one player from a kill.",
                "Shield a player from one kill and learn the killer's clues."),
            new("townofus.Seer",         "Seer",         RoleTeamTypes.Crewmate,
                new Color(0.65f, 0.45f, 1f, 1f),     "Investigate players to reveal their faction.",
                "Investigate other players to reveal their faction or role."),
            new("townofus.Vigilante",    "Vigilante",    RoleTeamTypes.Crewmate,
                new Color(0.95f, 0.65f, 0.25f, 1f),  "Shoot an Impostor; shooting a Crewmate kills you.",
                "Shoot a player during meetings with limited shots."),
            new("townofus.Altruist",     "Altruist",     RoleTeamTypes.Crewmate,
                new Color(0.95f, 0.5f, 0.72f, 1f),   "Revive a dead body — at the cost of your own life.",
                "Revive a dead body — at the cost of your own life."),
            new("townofus.Mayor",        "Mayor",        RoleTeamTypes.Crewmate,
                new Color(0.35f, 0.6f, 1f, 1f),      "Your vote counts double in meetings.",
                "Your vote counts for the Vote Bank."),
            new("townofus.Swapper",      "Swapper",      RoleTeamTypes.Crewmate,
                new Color(0.45f, 0.8f, 0.3f, 1f),    "During meetings, swap the votes of two players.",
                "During meetings, swap the votes of two players."),
            new("townofus.Spy",          "Spy",          RoleTeamTypes.Crewmate,
                new Color(0.85f, 0.7f, 0.35f, 1f),   "Get notified when someone vents or gets doused.",
                "Get intel on venting and dousing around the map."),
            new("townofus.Investigator", "Investigator", RoleTeamTypes.Crewmate,
                new Color(0.35f, 0.9f, 0.75f, 1f),   "See the footprints of other players.",
                "See the footprints of other players."),
            new("townofus.TimeLord",     "Time Lord",    RoleTeamTypes.Crewmate,
                new Color(0.55f, 0.65f, 0.95f, 1f),  "Rewind time to undo player movement.",
                "Rewind time to undo player movement."),
            new("townofus.Snitch",       "Snitch",       RoleTeamTypes.Crewmate,
                new Color(0.95f, 0.85f, 0.35f, 1f),  "Find the Impostors once your tasks are done.",
                "Complete all your tasks to reveal the Impostors with arrows."),

            // ---- Impostor ----
            new("townofus.Assassin",     "Assassin",     RoleTeamTypes.Impostor,
                new Color(0.95f, 0.15f, 0.18f, 1f),  "During meetings, guess another player's role. A correct guess kills them; a wrong guess kills you.",
                "Guess another player's role during a meeting. Wrong guesses kill you."),
            new("townofus.Janitor",      "Janitor",      RoleTeamTypes.Impostor,
                new Color(0.55f, 0.72f, 0.95f, 1f),  "Clean dead bodies so they cannot be reported.",
                "Clean dead bodies so they cannot be reported."),
            new("townofus.Morphling",    "Morphling",    RoleTeamTypes.Impostor,
                new Color(0.6f, 0.9f, 0.4f, 1f),     "Copy another player's appearance for a few seconds.",
                "Morph into another player's appearance for a short time."),
            new("townofus.Camouflager",  "Camouflager",  RoleTeamTypes.Impostor,
                new Color(0.55f, 0.6f, 0.95f, 1f),   "Turn everyone grey so identities are hidden.",
                "Turn everyone grey so identities are hidden."),
            new("townofus.Swooper",      "Swooper",      RoleTeamTypes.Impostor,
                new Color(0.4f, 0.45f, 0.6f, 1f),    "Become invisible for a short time.",
                "Become temporarily invisible."),
            new("townofus.Underdog",     "Underdog",     RoleTeamTypes.Impostor,
                new Color(0.9f, 0.4f, 0.4f, 1f),     "Faster kills when the Impostors are outnumbered.",
                "Your kill cooldown is reduced while outnumbered."),
            new("townofus.Undertaker",   "Undertaker",   RoleTeamTypes.Impostor,
                new Color(0.45f, 0.45f, 0.75f, 1f),  "Drag dead bodies away so they cannot be reported.",
                "Drag dead bodies away so they cannot be reported."),
            new("townofus.Miner",        "Miner",        RoleTeamTypes.Impostor,
                new Color(0.85f, 0.5f, 0.2f, 1f),    "Mine vents that connect only to each other.",
                "Mine vents that connect only to each other to move around the map."),

            // ---- Neutral ----
            new("townofus.Jester",       "Jester",       RoleTeamTypes.Neutral,
                new Color(0.86f, 0.35f, 0.95f, 1f),  "Get yourself voted out to win.",
                "Get yourself voted out to win."),
            new("townofus.Executioner",  "Executioner",  RoleTeamTypes.Neutral,
                new Color(0.45f, 0.9f, 0.85f, 1f),   "Get your target voted out to win.",
                "Get your target voted out to win. If your target dies another way, you convert."),
            new("townofus.Arsonist",     "Arsonist",     RoleTeamTypes.Neutral,
                new Color(1f, 0.45f, 0.15f, 1f),     "Douse players, then ignite them all to win.",
                "Douse everyone, then ignite to win."),
            new("townofus.Phantom",      "Phantom",      RoleTeamTypes.Neutral,
                new Color(0.75f, 0.75f, 0.85f, 1f),  "Complete all your tasks after death to win.",
                "Complete all your tasks after death to win."),
            new("townofus.Shifter",      "Shifter",      RoleTeamTypes.Neutral,
                new Color(0.75f, 0.55f, 0.95f, 1f),  "Swap roles and tasks with other players.",
                "Swap roles and tasks with another player. Shifting an Impostor kills you."),
            new("townofus.Glitch",       "The Glitch",   RoleTeamTypes.Neutral,
                new Color(0.45f, 0.95f, 0.35f, 1f),  "Mimic, hack, and kill everyone to be the last one standing.",
                "Mimic players, hack them, and kill everyone to be the last one standing."),
        };

        public static string TaskTextFor(string roleName)
        {
            foreach (var def in All)
                if (def.Name == roleName)
                    return def.TaskText;
            return "Complete your tasks and survive.";
        }
    }
}
