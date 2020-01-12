﻿using System;
using System.Collections.Generic;
using System.Numerics;
using Typography.OpenFont;

namespace Veldrid.TextRendering
{
    public enum TriangleKind
    {
        Solid,
        QuadraticCurve
    }

    public class GlyphTranslatorToVertices : IGlyphTranslator
    {
        public VertexPosition3Coord2[] ResultingVertices => vertices.ToArray();

        private float lastMoveX;
        private float lastMoveY;
        private float lastX;
        private float lastY;
        private short contourCount;
        private List<VertexPosition3Coord2> vertices;

        public void BeginRead(int contourCount)
        {
            Reset();
        }

        public void EndRead()
        {
        }

        public void MoveTo(float x, float y)
        {
            lastX = lastMoveX = x;
            lastY = lastMoveY = y;
            contourCount = 0;
        }

        public void CloseContour()
        {
            lastX = lastMoveX;
            lastY = lastMoveY;
            contourCount = 0;
        }

        // Quadratic bezier curve
        public void Curve3(float x1, float y1, float x2, float y2)
        {
            if (++contourCount >= 2)
            {
                AppendTriangle(lastMoveX, lastMoveY, lastX, lastY, x2, y2, TriangleKind.Solid);
            }

            AppendTriangle(lastX, lastY, x1, y1, x2, y2, TriangleKind.QuadraticCurve);
            lastX = x2;
            lastY = y2;
        }

        // Cubic bezier curve
        public void Curve4(float x1, float y1, float x2, float y2, float x3, float y3)
        {
            throw new NotImplementedException();
        }

        public void LineTo(float x, float y)
        {
            if (++contourCount >= 2)
            {
                AppendTriangle(lastMoveX, lastMoveY, lastX, lastY, x, y, TriangleKind.Solid);
            }

            lastX = x;
            lastY = y;
        }

        public void Reset()
        {
            vertices = new List<VertexPosition3Coord2>();
            lastMoveX = lastMoveY = lastX = lastY = 0;
            contourCount = 0;
        }

        private void AppendTriangle(float x1, float y1, float x2, float y2, float x3, float y3, TriangleKind kind)
        {
            if (kind == TriangleKind.Solid)
            {
                AppendVertex(x1, y1, 0, 1);
                AppendVertex(x2, y2, 0, 1);
                AppendVertex(x3, y3, 0, 1);
            }
            else
            {
                AppendVertex(x1, y1, 0, 0);
                AppendVertex(x2, y2, 0.5f, 0);
                AppendVertex(x3, y3, 1, 1);
            }
        }

        private void AppendVertex(float x, float y, float s, float t)
        {
            vertices.Add(new VertexPosition3Coord2(new Vector3(x, y, 0), new Vector2(s, t)));
        }
    }
}
