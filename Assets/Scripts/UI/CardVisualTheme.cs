using System;
using UnityEngine;

namespace Game29
{
    /// <summary>
    /// Central theme and asset provider for the 29 Game visual representation.
    /// Handles loading textures from Resources (TableFelt, CardFront, CardBack, SuitIcons),
    /// generates procedural antialiased rounded sprites, and provides color palettes and labels.
    /// </summary>
    public static class CardVisualTheme
    {
        // ── Color Palette ────────────────────────────────────────────────────────
        public static readonly Color ColorFeltDark    = new Color(0.04f, 0.20f, 0.11f, 1f);
        public static readonly Color ColorPanelDark   = new Color(0.06f, 0.10f, 0.15f, 0.90f);
        public static readonly Color ColorPanelHeader = new Color(0.10f, 0.16f, 0.24f, 0.95f);
        public static readonly Color ColorBorderGold  = new Color(0.85f, 0.70f, 0.28f, 1f);
        public static readonly Color ColorGold        = new Color(0.96f, 0.73f, 0.20f, 1f);
        public static readonly Color ColorGoldGlow    = new Color(1.00f, 0.84f, 0.35f, 0.6f);
        public static readonly Color ColorCyan        = new Color(0.22f, 0.74f, 0.97f, 1f);
        public static readonly Color ColorRedSuit     = new Color(0.89f, 0.18f, 0.18f, 1f);
        public static readonly Color ColorBlackSuit   = new Color(0.12f, 0.14f, 0.18f, 1f);
        public static readonly Color ColorCardPaper   = new Color(0.99f, 0.98f, 0.96f, 1f);

        // Point pill colors
        public static readonly Color ColorJackPts  = new Color(0.85f, 0.55f, 0.10f, 1f); // 3 pts (Gold)
        public static readonly Color ColorNinePts  = new Color(0.12f, 0.58f, 0.85f, 1f); // 2 pts (Blue)
        public static readonly Color ColorTenAcePts= new Color(0.20f, 0.70f, 0.40f, 1f); // 1 pt (Green)
        public static readonly Color ColorZeroPts  = new Color(0.40f, 0.45f, 0.55f, 0.85f); // 0 pt (Muted)

        // ── Textures & Sprites ───────────────────────────────────────────────────
        private static Sprite _tableFeltSprite;
        private static Sprite _cardFrontSprite;
        private static Sprite _cardBackSprite;
        private static Sprite[] _suitSprites; // [Heart, Diamond, Club, Spade]
        private static Sprite _roundedPanelSprite;
        private static Sprite _roundedCardSlotSprite;
        private static Sprite _pillBadgeSprite;
        private static Sprite _circleAvatarSprite;
        private static Font   _defaultFont;

        public static Sprite TableFelt => _tableFeltSprite ??= LoadOrGenerateFelt();
        public static Sprite CardFront => _cardFrontSprite ??= LoadOrGenerateCardFront();
        public static Sprite CardBack  => _cardBackSprite  ??= LoadOrGenerateCardBack();
        public static Sprite RoundedPanel => _roundedPanelSprite ??= CreateRoundedRectSprite(128, 128, 20, ColorPanelDark, ColorBorderGold, 3);
        public static Sprite RoundedCardSlot => _roundedCardSlotSprite ??= CreateRoundedRectSprite(128, 192, 16, new Color(0.03f, 0.14f, 0.08f, 0.65f), new Color(0.85f, 0.70f, 0.28f, 0.4f), 2);
        public static Sprite PillBadge => _pillBadgeSprite ??= CreateRoundedRectSprite(96, 40, 20, Color.white, Color.clear, 0);
        public static Sprite CircleAvatar => _circleAvatarSprite ??= CreateCircleSprite(128, ColorPanelHeader, ColorBorderGold, 4);

        public static Font GetFont()
        {
            if (_defaultFont != null) return _defaultFont;
            _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_defaultFont == null)
                _defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _defaultFont;
        }

        public static Color GetSuitColor(Suit suit)
        {
            return (suit == Suit.Hearts || suit == Suit.Diamonds) ? ColorRedSuit : ColorBlackSuit;
        }

        public static string GetSuitSymbol(Suit suit)
        {
            return suit switch
            {
                Suit.Hearts   => "♥",
                Suit.Diamonds => "♦",
                Suit.Clubs    => "♣",
                Suit.Spades   => "♠",
                _             => "?"
            };
        }

        public static string GetSuitName(Suit suit)
        {
            return suit switch
            {
                Suit.Hearts   => "Hearts",
                Suit.Diamonds => "Diamonds",
                Suit.Clubs    => "Clubs",
                Suit.Spades   => "Spades",
                _             => "Unknown"
            };
        }

        public static string GetRankString(Rank rank)
        {
            return rank switch
            {
                Rank.Seven => "7",
                Rank.Eight => "8",
                Rank.Nine  => "9",
                Rank.Ten   => "10",
                Rank.Jack  => "J",
                Rank.Queen => "Q",
                Rank.King  => "K",
                Rank.Ace   => "A",
                _          => rank.ToString()
            };
        }

        public static int GetPoints(Rank rank)
        {
            return rank switch
            {
                Rank.Jack => 3,
                Rank.Nine => 2,
                Rank.Ace  => 1,
                Rank.Ten  => 1,
                _         => 0
            };
        }

        public static Color GetPointsBadgeColor(int pts)
        {
            return pts switch
            {
                3 => ColorJackPts,
                2 => ColorNinePts,
                1 => ColorTenAcePts,
                _ => ColorZeroPts
            };
        }

        public static Sprite GetSuitSprite(Suit suit)
        {
            EnsureSuitSprites();
            if (_suitSprites != null && _suitSprites.Length == 4)
            {
                int idx = suit switch
                {
                    Suit.Hearts   => 0,
                    Suit.Diamonds => 1,
                    Suit.Clubs    => 2,
                    Suit.Spades   => 3,
                    _             => 0
                };
                if (_suitSprites[idx] != null) return _suitSprites[idx];
            }
            return null;
        }

        // ── Asset Loading ────────────────────────────────────────────────────────

        private static Sprite LoadOrGenerateFelt()
        {
            Sprite s = Resources.Load<Sprite>("TableFelt");
            if (s != null) return s;

            Texture2D tex = Resources.Load<Texture2D>("TableFelt");
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            // Fallback: procedural luxurious dark green casino felt
            tex = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.05f;
                    tex.SetPixel(x, y, new Color(0.04f + noise, 0.22f + noise, 0.12f + noise, 1f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }

        private static Sprite LoadOrGenerateCardFront()
        {
            Sprite s = Resources.Load<Sprite>("CardFront");
            if (s != null) return s;

            Texture2D tex = Resources.Load<Texture2D>("CardFront");
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            return CreateRoundedRectSprite(180, 260, 20, ColorCardPaper, ColorBorderGold, 4);
        }

        private static Sprite LoadOrGenerateCardBack()
        {
            Sprite s = Resources.Load<Sprite>("CardBack");
            if (s != null) return s;

            Texture2D tex = Resources.Load<Texture2D>("CardBack");
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

            return CreateRoundedRectSprite(180, 260, 20, new Color(0.10f, 0.15f, 0.28f), ColorBorderGold, 4);
        }

        private static void EnsureSuitSprites()
        {
            if (_suitSprites != null) return;
            _suitSprites = new Sprite[4];

            Texture2D tex = Resources.Load<Texture2D>("SuitIcons");
            if (tex == null) return;

            // SuitIcons is a 2x2 grid:
            // Top-left: Heart, Top-right: Diamond, Bottom-left: Club, Bottom-right: Spade
            int halfW = tex.width / 2;
            int halfH = tex.height / 2;

            _suitSprites[0] = Sprite.Create(tex, new Rect(0, halfH, halfW, halfH), new Vector2(0.5f, 0.5f));
            _suitSprites[1] = Sprite.Create(tex, new Rect(halfW, halfH, halfW, halfH), new Vector2(0.5f, 0.5f));
            _suitSprites[2] = Sprite.Create(tex, new Rect(0, 0, halfW, halfH), new Vector2(0.5f, 0.5f));
            _suitSprites[3] = Sprite.Create(tex, new Rect(halfW, 0, halfW, halfH), new Vector2(0.5f, 0.5f));
        }

        // ── Procedural Sprite Generation ─────────────────────────────────────────

        public static Sprite CreateRoundedRectSprite(int w, int h, int radius, Color fill, Color border, int borderWidth)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            float r = radius;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Distance from closest corner
                    float cx = (x < r) ? r : (x > w - 1 - r) ? w - 1 - r : x;
                    float cy = (y < r) ? r : (y > h - 1 - r) ? h - 1 - r : y;
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));

                    if (dist > r)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (borderWidth > 0 && (dist >= r - borderWidth || x < borderWidth || x >= w - borderWidth || y < borderWidth || y >= h - borderWidth))
                    {
                        // Border antialias
                        float alpha = Mathf.Clamp01(r - dist);
                        tex.SetPixel(x, y, new Color(border.r, border.g, border.b, border.a * alpha));
                    }
                    else
                    {
                        float alpha = Mathf.Clamp01(r - dist);
                        tex.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, fill.a * (dist > r - 1 ? alpha : 1f)));
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        public static Sprite CreateCircleSprite(int size, Color fill, Color border, int borderWidth)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            float center = (size - 1) * 0.5f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist > radius)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (borderWidth > 0 && dist >= radius - borderWidth)
                    {
                        float alpha = Mathf.Clamp01(radius - dist);
                        tex.SetPixel(x, y, new Color(border.r, border.g, border.b, border.a * alpha));
                    }
                    else
                    {
                        float alpha = Mathf.Clamp01(radius - dist);
                        tex.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, fill.a * (dist > radius - 1 ? alpha : 1f)));
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
