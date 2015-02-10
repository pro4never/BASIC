using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LeagueSharp;
using LeagueSharp.Common;

using SharpDX;

using Color = System.Drawing.Color;

namespace BASICBrand
{
    public static class Program
    {
        public const string CHAMP_NAME = "Brand";
        public static Obj_AI_Hero player = ObjectManager.Player;

        public static Spell Q, W, E, R;
        public static readonly List<Spell> spellList = new List<Spell>();

        private const int BOUNCE_RADIUS = 450;

        public static MenuWrapper menu;

        // Menu links
        internal static Dictionary<string, MenuWrapper.BoolLink> boolLinks = new Dictionary<string, MenuWrapper.BoolLink>();
        internal static Dictionary<string, MenuWrapper.CircleLink> circleLinks = new Dictionary<string, MenuWrapper.CircleLink>();
        internal static Dictionary<string, MenuWrapper.KeyBindLink> keyLinks = new Dictionary<string, MenuWrapper.KeyBindLink>();
        internal static Dictionary<string, MenuWrapper.SliderLink> sliderLinks = new Dictionary<string, MenuWrapper.SliderLink>();

        public static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            // Validate champ, detuks should do this everywhere :D
            if (player.ChampionName != CHAMP_NAME)
                return;

            // Initialize spells
            Q = new Spell(SpellSlot.Q, 1050);
            W = new Spell(SpellSlot.W, 900);
            E = new Spell(SpellSlot.E, 625);
            R = new Spell(SpellSlot.R, 750);

            // Add to spell list
            spellList.AddRange(new[] { Q, W, E, R });

            // Finetune spells
            Q.SetSkillshot(0.25f, 80, 1200, true, SkillshotType.SkillshotLine);
            W.SetSkillshot(1, 200, float.MaxValue, false, SkillshotType.SkillshotCircle);
            E.SetTargetted(0.25f, float.MaxValue);
            R.SetTargetted(0.25f, 1000);

            // Setup menu
            SetuptMenu();
            
            // Register event handlers
            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            // Harass
            if (keyLinks["harassActive"].Value.Active)
                OnHarass();
        }

        private static void OnHarass()
        {
            var eTarget = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Magical);
            if (eTarget != null)
                if (E.IsReady() && E.IsInRange(eTarget.ServerPosition))
                    E.CastOnUnit(eTarget, true);

            var qTarget = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            if (qTarget != null && qTarget.IsAblazed() && Q.IsReady() && Q.IsInRange(qTarget.ServerPosition))
                Q.CastIfHitchanceEquals(qTarget, HitChance.VeryHigh);

            var wTarget = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
            if (wTarget != null && wTarget.IsAblazed() && W.IsReady() && W.IsInRange(wTarget.ServerPosition))
                W.CastIfHitchanceEquals(wTarget, HitChance.VeryHigh);
        }

        // TODO: DFG handling and so on :P
        public static double GetMainComboDamage(Obj_AI_Base target)
        {
            double damage = player.GetAutoAttackDamage(target);

            if (Q.IsReady())
                damage += player.GetSpellDamage(target, SpellSlot.Q);

            if (W.IsReady())
                damage += player.GetSpellDamage(target, SpellSlot.W) * (target.IsAblazed() ? 1.25 : 1);

            if (E.IsReady())
                damage += player.GetSpellDamage(target, SpellSlot.E);

            if (R.IsReady())
                damage += player.GetSpellDamage(target, SpellSlot.R);

            if (player.HasIgnite())
                damage += player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);

            return damage;
        }

        public static bool IsMainComboKillable(this Obj_AI_Base target)
        {
            return GetMainComboDamage(target) > target.Health;
        }

        public static double GetBounceComboDamage(Obj_AI_Base target)
        {
            double damage = GetMainComboDamage(target);

            if (R.IsReady())
                damage += player.GetSpellDamage(target, SpellSlot.R);

            return damage;
        }

        public static bool IsBounceComboKillable(this Obj_AI_Base target)
        {
            return GetBounceComboDamage(target) > target.Health;
        }

        public static float GetHPBarComboDamage(Obj_AI_Hero target)
        {
            return (float)GetMainComboDamage(target);
        }

        public static bool IsAblazed(this Obj_AI_Base target)
        {
            return target.HasBuff("brandablaze", true);
        }

        public static float Cooldown(this Spell spell)
        {
            return player.Spellbook.GetSpell(spell.Slot).Cooldown;
        }

        public static bool HasIgnite(this Obj_AI_Hero target, bool checkReady = true)
        {
            if (target.IsMe)
            {
                var ignite = player.Spellbook.GetSpell(player.GetSpellSlot("SummonerDot"));
                return ignite != null && ignite.Slot != SpellSlot.Unknown && (checkReady ? player.Spellbook.CanUseSpell(ignite.Slot) == SpellState.Ready && player.Distance(target, true) < 400 * 400 : true);
            }
            return false;
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            // All circles
            foreach (var circle in circleLinks.Values.Select(link => link.Value))
            {
                if (circle.Active)
                    Render.Circle.DrawCircle(player.Position, circle.Radius, circle.Color);
            }
        }

        private static void SetuptMenu()
        {
            // Initialize the menu
            menu = new MenuWrapper("[BASIC] " + CHAMP_NAME);

            // Combo
            var combo = menu.MainMenu.AddSubMenu("Settings");
            keyLinks.Add("harassActive", combo.AddLinkedKeyBind("HarassActive", 0x21, KeyBindType.Toggle));
            //Page up to toggle

            // Drawings
            var drawings = menu.MainMenu.AddSubMenu("Drawings");
            circleLinks.Add("drawRangeQ", drawings.AddLinkedCircle("Q range", true, Color.FromArgb(150, Color.IndianRed), Q.Range));
            circleLinks.Add("drawRangeW", drawings.AddLinkedCircle("W range", true, Color.FromArgb(150, Color.IndianRed), W.Range));
            circleLinks.Add("drawRangeE", drawings.AddLinkedCircle("E range", false, Color.FromArgb(150, Color.DarkRed), E.Range));
            circleLinks.Add("drawRangeR", drawings.AddLinkedCircle("R range", false, Color.FromArgb(150, Color.Red), R.Range));
        }
    }
}
