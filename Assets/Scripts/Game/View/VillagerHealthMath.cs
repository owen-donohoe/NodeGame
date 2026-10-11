using System;

namespace NodeWar.View
{
    /// <summary>Read-only health presentation and layout from final sprite bounds.</summary>
    public static class VillagerHealthMath
    {
        public static float Fraction(int hp, int maxHP)
            => maxHP <= 0 ? 0f : Math.Max(0f, Math.Min(1f, (float)hp / maxHP));

        public static bool Visible(int hp, int maxHP, bool alive, bool consumed, bool alwaysVisible)
            => alive && !consumed && hp > 0 && maxHP > 0 && (alwaysVisible || hp < maxHP);

        public static (float Width, float Height, float BottomY) Layout(float spriteWidth, float spriteHeight, float topY)
            => (spriteWidth * 0.65f, spriteHeight * 0.08f, topY + spriteHeight * 0.10f);
    }
}
