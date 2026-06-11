using DieselExileTools.Common;
using ExileCore2;
using ImGuiNET;
using SColor = System.Drawing.Color;
using SVector2 = System.Numerics.Vector2;
using Graphics = ExileCore2.Graphics;

namespace DieselExileTools;
public static partial class DXT {
    public readonly struct ColorSegment : IEquatable<ColorSegment>
    {
        public string Text { get; }
        public SColor Color { get; }
        public ColorSegment(string text, SColor color) { Text = text; Color = color; }
        /// <summary>Creates a new segment with the provided text, defaulting to the Draw call's default color.</summary>
        public ColorSegment(string text) : this(text, SColor.Empty) { }
        public bool Equals(ColorSegment other) => Text == other.Text && Color.Equals(other.Color);
        public override bool Equals(object? obj) => obj is ColorSegment other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Text, Color);
    }

    public class ColoredText
    {
        public IReadOnlyList<ColorSegment> Segments { get; } // immutable so we can cache values like width
        private float? _width;
        private float? _height;
        private SVector2? _size;

        /// <summary>Gets the total width of the colored text in pixels (cached after first calculation).</summary>
        public float Width {
            get {
                if (_width == null) {
                    float w = 0;
                    foreach (var seg in Segments)
                        w += ImGui.CalcTextSize(seg.Text).X;
                    _width = w;
                }
                return _width.Value;
            }
        }

        /// <summary>Gets the height of the colored text in pixels (cached after first calculation).</summary>
        public float Height {
            get {
                _height ??= ImGui.CalcTextSize("A").Y;
                return _height.Value;
            }
        }

        /// <summary>Gets the total size (width and height) of the colored text in pixels (cached after first calculation).</summary>
        public SVector2 Size {
            get {
                if (_size == null)
                    _size = new SVector2(Width, Height);
                return _size.Value;
            }
        }

        /// <summary>Gets the number of color segments in this text.</summary>
        public int Count => Segments.Count;

        public ColoredText(string input) {
            if (input is null) input = string.Empty;
            var segments = ParseSegments(input);
            Segments = segments.AsReadOnly();
        }

        public ColoredText(string input, SColor color) : this(new List<ColorSegment> { new(input ?? string.Empty, color) }) { }

        public ColoredText(IEnumerable<ColorSegment> segments) => Segments = segments.ToList().AsReadOnly();

        /// <summary>Returns the concatenated plain text (without color information).</summary>
        public string ToUncoloredString() {
            var sb = new System.Text.StringBuilder();
            foreach (var seg in Segments)
                sb.Append(seg.Text);
            return sb.ToString();
        }
        /// <summary>Returns the concatenated plain text (without color information).</summary>
        public override string ToString() => ToUncoloredString();

        /// <summary>
        /// Draws the colored text at the specified position using the given Graphics.
        /// If a segment does not specify a color, the provided <paramref name="defaultColor"/> is used; if not set, white is used.
        /// </summary>
        public void Draw(Graphics g, SVector2 pos, SColor? defaultColor = null) {
            // Keep your exact default logic
            defaultColor ??= SColor.White;
            float x = pos.X;
            foreach (var seg in Segments) {
                // Keep your exact IsEmpty fallback logic
                var color = seg.Color.IsEmpty ? defaultColor.Value : seg.Color;

                g.DrawText(seg.Text, new SVector2(x, pos.Y), color);

                // Use your current Graphics measure method
                x += g.MeasureText(seg.Text).X;
            }
        }


        /// <summary>Returns a new instance truncated to fit within the specified width, appending an ellipsis if needed.</summary>
        public ColoredText Ellipsize(float maxWidth, string ellipsis = "...") {
            var result = new List<ColorSegment>();
            float usedWidth = 0;
            float ellipsisWidth = ImGui.CalcTextSize(ellipsis).X;
            foreach (var seg in Segments) {
                float segWidth = ImGui.CalcTextSize(seg.Text).X;
                if (usedWidth + segWidth > maxWidth) {
                    // Need to truncate this segment
                    int charCount = 0;
                    float partialWidth = 0;
                    foreach (char c in seg.Text) {
                        float charWidth = ImGui.CalcTextSize(c.ToString()).X;
                        if (usedWidth + partialWidth + charWidth + ellipsisWidth > maxWidth)
                            break;
                        partialWidth += charWidth;
                        charCount++;
                    }
                    if (charCount > 0)
                        result.Add(new ColorSegment(seg.Text.Substring(0, charCount) + ellipsis, seg.Color));
                    else if (result.Count > 0)
                        result[result.Count - 1] = new ColorSegment(result.Last().Text + ellipsis, result.Last().Color);
                    else
                        result.Add(new ColorSegment(ellipsis, seg.Color));
                    break;
                }
                else {
                    result.Add(seg);
                    usedWidth += segWidth;
                }
            }
            // If no truncation was needed, return this
            if (result.Count == Segments.Count && result.SequenceEqual(Segments))
                return this;
            return new ColoredText(result);
        }

        // Private parser
        private static List<ColorSegment> ParseSegments(string input) {
            var segments = new List<ColorSegment>();
            var colorStack = new Stack<SColor>();
            colorStack.Push(SColor.Empty); // Use Empty to indicate "no color"

            int i = 0;
            int start = 0;
            while (i < input.Length) {
                if (input[i] == '|' && i + 9 < input.Length && input[i + 1] == 'c') {
                    if (i > start)
                        segments.Add(new ColorSegment(input[start..i], colorStack.Peek()));
                    string hex = input.Substring(i + 2, 8);
                    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                    byte a = Convert.ToByte(hex.Substring(6, 2), 16);
                    colorStack.Push(SColor.FromArgb(a, r, g, b));
                    i += 10;
                    start = i;
                }
                else if (input[i] == '|' && i + 1 < input.Length && input[i + 1] == 'r') {
                    if (i > start)
                        segments.Add(new ColorSegment(input[start..i], colorStack.Peek()));
                    if (colorStack.Count > 1)
                        colorStack.Pop();
                    i += 2;
                    start = i;
                }
                else {
                    i++;
                }
            }
            if (i > start)
                segments.Add(new ColorSegment(input[start..i], colorStack.Peek()));

            return segments;
        }




    }
}
