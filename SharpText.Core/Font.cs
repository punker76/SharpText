﻿using System;
using System.Collections.Generic;
using System.IO;
using ICSharpCode.SharpZipLib.Zip.Compression;
using Typography.OpenFont;
using Typography.OpenFont.Extensions;
using Typography.OpenFont.WebFont;
using Typography.TextLayout;

namespace SharpText.Core
{
    /// <summary>
    /// Contains measurement information for a piece of text
    /// </summary>
    public struct MeasurementInfo
    {
        public float Ascender;
        public float Descender;
        public float LineHeight;
        public float[] AdvanceWidths;
    }

    /// <summary>
    /// Contains vertices and bounds information for a piece of text
    /// </summary>
    public struct StringVertices
    {
        public VertexPosition3Coord2[][] Vertices;
        public BoundingRectangle Bounds;
    }

    /// <summary>
    /// Represents a font file and its associated glyphs and allows access to glyph vertices
    /// </summary>
    public class Font
    {
        public float FontSizeInPoints { get; private set; }
        public float FontSizeInPixels => FontSizeInPoints * POINTS_TO_PIXELS;
        public string Name => typeface.Name;

        private const float POINTS_TO_PIXELS = 4f / 3f;
        private const float PIXELS_TO_POINTS = 3f / 4f;

        private readonly Typeface typeface;
        private readonly Dictionary<char, Glyph> loadedGlyphs;
        private readonly GlyphPathBuilder pathBuilder;
        private readonly GlyphTranslatorToVertices pathTranslator;

        private float TotalHeight => (typeface.Bounds.YMax - typeface.Bounds.YMin) * (FontSizeInPixels / typeface.UnitsPerEm);

        /// <summary>
        /// Create a new font instance
        /// </summary>
        /// <param name="filePath">Path to the font file</param>
        /// <param name="fontSizeInPixels">The desired font size in pixels</param>
        public Font(string filePath, float fontSizeInPixels)
        {
            SetupWoffDecompressorIfRequired();

            FontSizeInPoints = fontSizeInPixels * PIXELS_TO_POINTS;
            loadedGlyphs = new Dictionary<char, Glyph>();

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                var reader = new OpenFontReader();
                typeface = reader.Read(fs);
            }

            pathBuilder = new GlyphPathBuilder(typeface);
            pathTranslator = new GlyphTranslatorToVertices();
        }

        /// <summary>
        /// Create a new font instance
        /// </summary>
        /// <param name="fontStream">Stream to the font data</param>
        /// <param name="fontSizeInPixels">The desired font size in pixels</param>
        public Font(Stream fontStream, float fontSizeInPixels)
        {
            SetupWoffDecompressorIfRequired();

            FontSizeInPoints = fontSizeInPixels * PIXELS_TO_POINTS;
            loadedGlyphs = new Dictionary<char, Glyph>();

            var reader = new OpenFontReader();
            typeface = reader.Read(fontStream);

            pathBuilder = new GlyphPathBuilder(typeface);
            pathTranslator = new GlyphTranslatorToVertices();
        }

        /// <summary>
        /// Returns vertices for the specified character in pixel units
        /// </summary>
        /// <param name="character">The character</param>
        /// <returns>Vertices in pixel units</returns>
        public VertexPosition3Coord2[] GetVerticesForCharacter(char character)
        {
            var glyph = GetGlyphByCharacter(character);

            pathBuilder.BuildFromGlyph(glyph, FontSizeInPoints);

            pathBuilder.ReadShapes(pathTranslator);
            var vertices = pathTranslator.ResultingVertices;

            return vertices;
        }

        /// <summary>
        /// Returns vertices and bounds info for the given text
        /// </summary>
        /// <param name="text">The text</param>
        /// <returns>Vertices and bounds info</returns>
        public StringVertices GetVerticesForString(string text)
        {
            var stringData = new StringVertices();
            stringData.Vertices = new VertexPosition3Coord2[text.Length][];

            for (var i = 0; i < text.Length; i++)
            {
                var vertices = GetVerticesForCharacter(text[i]);
                for (var j = 0; j < vertices.Length; j++)
                {
                    stringData.Bounds.Include(vertices[j].Position.X, vertices[j].Position.Y);
                }

                stringData.Vertices[i] = vertices;
            }

            // Move up/down the glyphs to align them to Y=0
            if (typeface.Bounds.YMin != 0)
            {
                var scale = FontSizeInPixels / TotalHeight;
                var offset = typeface.Bounds.YMin * (FontSizeInPixels / typeface.UnitsPerEm);
                for (var i = 0; i < text.Length; i++)
                {
                    for (var j = 0; j < stringData.Vertices[i].Length; j++)
                    {
                        stringData.Vertices[i][j].Position.X *= scale;
                        stringData.Vertices[i][j].Position.Y *= scale;
                        stringData.Vertices[i][j].Position.Y -= offset + FontSizeInPixels;
                    }
                }

                stringData.Bounds.StartY -= offset;
                stringData.Bounds.EndY -= offset;
                stringData.Bounds.StartY *= scale;
                stringData.Bounds.EndY *= scale;
            }

            return stringData;
        }

        /// <summary>
        /// Return glyph advance distances along the X axis to layout text
        /// </summary>
        /// <param name="text">Text to layout</param>
        /// <returns>Advance distances for each glyph in pixel units</returns>
        public MeasurementInfo GetMeasurementInfoForString(string text)
        {
            var layout = new GlyphLayout();
            layout.Typeface = typeface;

            var measure = layout.LayoutAndMeasureString(text.ToCharArray(), 0, text.Length, FontSizeInPoints);
            var lineHeight = typeface.CalculateLineSpacing(LineSpacingChoice.TypoMetric) * (FontSizeInPixels / typeface.UnitsPerEm);

            var glyphPositions = layout.ResultUnscaledGlyphPositions;
            var advanceWidths = new float[glyphPositions.Count];
            var scale = FontSizeInPixels / TotalHeight;
            for (var i = 0; i < glyphPositions.Count; i++)
            {
                glyphPositions.GetGlyph(i, out var advanceW);
                advanceWidths[i] = advanceW * (FontSizeInPixels / typeface.UnitsPerEm) * scale;
            }

            return new MeasurementInfo
            {
                Ascender = measure.AscendingInPx,
                Descender = measure.DescendingInPx,
                AdvanceWidths = advanceWidths,
                LineHeight = lineHeight
            };
        }

        /// <summary>
        /// Get a glyph from the font by character
        /// </summary>
        /// <param name="character">The character</param>
        /// <returns>The glyph for the character</returns>
        private Glyph GetGlyphByCharacter(char character)
        {
            if (loadedGlyphs.ContainsKey(character))
                return loadedGlyphs[character];

            var glyphIndex = typeface.GetGlyphIndex(character);
            var glyph = typeface.GetGlyph(glyphIndex);

            loadedGlyphs.Add(character, glyph);

            return glyph;
        }

        /// <summary>
        /// The initial WOFF decompressor is null and throws an exception
        /// So we use SharpZipLib to inflate the file
        /// </summary>
        private static void SetupWoffDecompressorIfRequired()
        {
            if (WoffDefaultZlibDecompressFunc.DecompressHandler != null)
                return;

            WoffDefaultZlibDecompressFunc.DecompressHandler = (byte[] compressedBytes, byte[] decompressedResult) =>
            {
                try
                {
                    var inflater = new Inflater();
                    inflater.SetInput(compressedBytes);
                    inflater.Inflate(decompressedResult);

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    return false;
                }
            };
        }
    }
}
