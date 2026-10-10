using UnityEngine;
using NodeWar.Simulation;
namespace NodeWar.View
{
    /// <summary>Small code-only letter stickers for missing flat art. No imported asset required.</summary>
    public static class DistrictFallbackArt
    {
        private static readonly Sprite[] sprites=new Sprite[(int)DistrictType.Workshop+1];
        public static Sprite Sticker(DistrictType type)
        {
            if(!DistrictRoster.IsActive(type)) return null;
            int index=(int)type; if(sprites[index]!=null) return sprites[index];
            string letters=DistrictFallback.Describe(type).Monogram.ToUpperInvariant();
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false) { hideFlags=HideFlags.HideAndDontSave, filterMode=FilterMode.Point };
            var pixels=new Color32[32*32];
            int scale=letters.Length==1?3:2;
            int width=letters.Length*6*scale-scale;
            for(int letter=0;letter<letters.Length;letter++)
            {
                string rows=Glyph(letters[letter]);
                for(int y=0;y<7;y++) for(int x=0;x<5;x++)
                    if(rows[y*5+x]=='1')
                        for(int dy=0;dy<scale;dy++) for(int dx=0;dx<scale;dx++)
                            pixels[((32-7*scale)/2+(6-y)*scale+dy)*32+(32-width)/2+letter*6*scale+x*scale+dx]=new Color32(255,255,255,255);
            }
            texture.SetPixels32(pixels); texture.Apply(false,true);
            var sprite=Sprite.Create(texture,new Rect(0,0,32,32),new Vector2(0.5f,0.5f),32);
            sprite.hideFlags=HideFlags.HideAndDontSave; sprite.name=DistrictFallback.Describe(type).Name+" fallback";
            return sprites[index]=sprite;
        }
        private static string Glyph(char letter)
        {
            switch(letter)
            {
                case 'V': return "10001100011000110001100010101000100";
                case 'W': return "10001100011000110101101011101110001";
                case 'T': return "11111001000010000100001000010000100";
                case 'P': return "11110100011000111110100001000010000";
                case 'B': return "11110100011000111110100011000111110";
                case 'I': return "11111001000010000100001000010011111";
                case 'F': return "11111100001000011110100001000010000";
                case 'S': return "01111100001000001110000010000111110";
                case 'C': return "01111100001000010000100001000001111";
                case 'X': return "10001100010101000100010101000110001";
                case 'M': return "10001110111010110101100011000110001";
                case 'A': return "01110100011000111111100011000110001";
                case 'O': return "01110100011000110001100011000101110";
                default: return "11110000010001000100001000000000100";
            }
        }
    }
}
