namespace Manimal.LegArmor
{
    // hardcoded layout values for the corpse-loot view. previously these
    // were BepInEx ConfigEntry<float>s exposed in F12 ConfigurationManager;
    // we tuned via the sliders, locked in the numbers, then converted to
    // plain static fields so users don't accidentally drift the layout
    // and we don't bother writing a cfg file just for read-once values.
    //
    // detection (LegArmor + Trenchfoot-BeltSlot installed together) still
    // picks between two value sets: Solo for leg-armor-only installs and
    // WithBeltSlot when Trenchfoot-BeltSlot is also present (because the
    // belt row shifts the containers panel and we need different offsets).
    //
    // named LegArmorConfig (not just Config) because BaseUnityPlugin
    // already has an instance property called "Config" that shadows
    // unqualified type references inside Plugin.cs.
    public static class LegArmorConfig
    {
        // active values - set by Init() at startup. patches read these
        // directly. these were ConfigEntry<float>.Value reads before; now
        // plain floats with no .Value indirection.
        public static float PrimaryRowOffsetY;
        public static float SecondaryRowOffsetY;
        public static float TacticalRigSpacerHeight;
        public static float BackpackSpacerHeight;
        public static float PocketsSpacerHeight;
        public static float TacticalRigSlotOffsetY;
        public static float BackpackSlotOffsetY;
        public static float PocketsSlotOffsetY;

        private struct Defaults
        {
            public float PrimaryRow;
            public float SecondaryRow;
            public float RigSpacer;
            public float BackpackSpacer;
            public float PocketsSpacer;
            public float RigSlot;
            public float BackpackSlot;
            public float PocketsSlot;
        }

        // tuned for LegArmor only - matches the values we shipped before
        // belt-slot compatibility was a concern.
        private static readonly Defaults Solo = new Defaults
        {
            PrimaryRow = -86f,
            SecondaryRow = -86f,
            RigSpacer = 0f,
            BackpackSpacer = 0f,
            PocketsSpacer = 0f,
            RigSlot = -32f,
            BackpackSlot = 54f,
            PocketsSlot = 10f,
        };

        // tuned for LegArmor + Trenchfoot-BeltSlot. only BackpackSlot is
        // non-zero relative to Solo - the belt row pushes backpack further
        // down (54 -> 100). PocketsSlot is 0 here because Belt mod owns
        // the pockets offset in this case (see Trenchfoot-BeltSlot's
        // Settings.WithLegArmor.PocketsOffsetY = 60).
        private static readonly Defaults WithBeltSlot = new Defaults
        {
            PrimaryRow = -86f,
            SecondaryRow = -86f,
            RigSpacer = 0f,
            BackpackSpacer = 0f,
            PocketsSpacer = 0f,
            RigSlot = -32f,
            BackpackSlot = 100f,
            PocketsSlot = 0f,
        };

        public static void Init(bool isBeltSlotInstalled = false)
        {
            var d = isBeltSlotInstalled ? WithBeltSlot : Solo;
            PrimaryRowOffsetY = d.PrimaryRow;
            SecondaryRowOffsetY = d.SecondaryRow;
            TacticalRigSpacerHeight = d.RigSpacer;
            BackpackSpacerHeight = d.BackpackSpacer;
            PocketsSpacerHeight = d.PocketsSpacer;
            TacticalRigSlotOffsetY = d.RigSlot;
            BackpackSlotOffsetY = d.BackpackSlot;
            PocketsSlotOffsetY = d.PocketsSlot;
        }
    }
}
