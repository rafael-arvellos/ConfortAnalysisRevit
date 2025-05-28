using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Geometries;

namespace SpatialAnalysis
{
    public struct CellIndex
    {
        public int I, J, K;
        public CellIndex(int i, int j, int k) { I = i; J = j; K = k; }
        public bool Equals(CellIndex other) =>
            I == other.I && J == other.J && K == other.K;
        public override bool Equals(object? obj) =>
            obj is CellIndex other && Equals(other);
        public override int GetHashCode() =>
            HashCode.Combine(I, J, K);
    }

    public class CellData
    {
        public STRtree<BoundingBoxXYZ> RTree { get; }
        public CellData() => RTree = new STRtree<BoundingBoxXYZ>();
    }

    public static class SpatialGrid
    {

        public static (Dictionary<CellIndex, CellData> cells, XYZ gridOrigin, int ICount, int JCount, int KCount) ComputeGrid(double step, BoundingBoxXYZ bounds)
        {
            var cells = new Dictionary<CellIndex, CellData>();
            XYZ origin = bounds.Min;

            // Cálculo de quantas células em cada direção
            double dx = bounds.Max.X - origin.X;
            double dy = bounds.Max.Y - origin.Y;
            double dz = bounds.Max.Z - origin.Z;

            int iCount = (int)Math.Ceiling(dx / step);
            int jCount = (int)Math.Ceiling(dy / step);
            int kCount = (int)Math.Ceiling(dz / step);

            for (int i = 0; i < iCount; i++)
                for (int j = 0; j < jCount; j++)
                    for (int k = 0; k < kCount; k++)
                    {
                        cells[new CellIndex(i, j, k)] = new CellData();
                    }

            return (cells, origin, iCount, jCount, kCount);
        }

        public static CellIndex HashPointToCell(XYZ pt, XYZ gridOrigin, double step, int iCount, int jCount, int kCount)
        {
            // Calcula deslocamento do ponto em relação à origem da grade
            double dx = pt.X - gridOrigin.X;
            double dy = pt.Y - gridOrigin.Y;
            double dz = pt.Z - gridOrigin.Z;

            // Converte para índices de célula usando divisão inteira (Floor)
            int i = (int)Math.Floor(dx / step);
            int j = (int)Math.Floor(dy / step);
            int k = (int)Math.Floor(dz / step);

            // Clamp para não sair da malha            
            i = Math.Max(0, Math.Min(i, iCount - 1));
            j = Math.Max(0, Math.Min(j, jCount - 1));
            k = Math.Max(0, Math.Min(k, kCount - 1));


            return new CellIndex(i, j, k);
        }
    }
    public static class SpatialIndexer
    {
        public static void BuildSpatialIndex(
            Dictionary<CellIndex, CellData> cells,
            XYZ gridOrigin, double step,
            int iCount, int jCount, int kCount,
            Dictionary<Solid, BoundingBoxXYZ> solidBBoxes)
        {
            foreach (var bbox in solidBBoxes.Values)
            {
                // Calcula índices min/max das células que o bbox ocupa
                int iMin = (int)Math.Floor((bbox.Min.X - gridOrigin.X) / step);
                int jMin = (int)Math.Floor((bbox.Min.Y - gridOrigin.Y) / step);
                int kMin = (int)Math.Floor((bbox.Min.Z - gridOrigin.Z) / step);

                int iMax = (int)Math.Floor((bbox.Max.X - gridOrigin.X) / step);
                int jMax = (int)Math.Floor((bbox.Max.Y - gridOrigin.Y) / step);
                int kMax = (int)Math.Floor((bbox.Max.Z - gridOrigin.Z) / step);

                // Clamp aos limites da malha
                iMin = Math.Max(0, Math.Min(iMin, iCount - 1));
                jMin = Math.Max(0, Math.Min(jMin, jCount - 1));
                kMin = Math.Max(0, Math.Min(kMin, kCount - 1));
                iMax = Math.Max(0, Math.Min(iMax, iCount - 1));
                jMax = Math.Max(0, Math.Min(jMax, jCount - 1));
                kMax = Math.Max(0, Math.Min(kMax, kCount - 1));

                // Insere o bbox nas células correspondentes
                for (int i = iMin; i <= iMax; i++)
                    for (int j = jMin; j <= jMax; j++)
                        for (int k = kMin; k <= kMax; k++)
                        {
                            var idx = new CellIndex(i, j, k);
                            // Acesso direto: aqui cells[idx] nunca será nulo, pois
                            // pré-populamos todas as células em ComputeGrid
                            var cellData = cells[idx];

                            // Cria o envelope 2D para indexação no R-tree
                            var env = new Envelope(
                                bbox.Min.X, bbox.Max.X,
                                bbox.Min.Y, bbox.Max.Y);

                            // Insere o BoundingBoxXYZ no mini-R-tree da célula
                            cellData.RTree.Insert(env, bbox);
                        }
            }

            // Constrói as árvores para consultas eficientes
            foreach (var cell in cells.Values)
                cell.RTree.Build();
        }
    }

    public static class PointFilter
    {
        public static List<XYZ> FilterPointsBySolid(
            IList<XYZ> points,
            Dictionary<CellIndex, CellData> cells,
            XYZ origin, double step,
            int iCount, int jCount, int kCount,
            IDictionary<Solid, BoundingBoxXYZ> solidBBoxes)
        {
            // 1) Tesselação e BVH: precompute para cada sólido
            var solidMeshes = new Dictionary<Solid, TriangulatedSolidOrShell>();
            var meshBVH = new Dictionary<Solid, AABBNode>();
            var controls = new SolidOrShellTessellationControls();
            controls.Accuracy = 0.01;  // 1 cm de desvio absoluto
            controls.LevelOfDetail = 0.1;
            foreach (var solid in solidBBoxes.Keys)
            {
                var mesh = SolidUtils.TessellateSolidOrShell(solid, controls);
                solidMeshes[solid] = mesh;                                                 // :contentReference[oaicite:3]{index=3}
                meshBVH[solid] = AABBNode.Build(mesh);                                  // 
            }
            controls.Dispose();

            // Inverte o mapeamento para encontrar o Solid a partir do bbox
            var bboxToSolid = new Dictionary<BoundingBoxXYZ, Solid>();
            foreach (var kvp in solidBBoxes)
                bboxToSolid[kvp.Value] = kvp.Key;

            var freePoints = new List<XYZ>(points.Count);

            foreach (var pt in points)
            {
                // a) localiza célula e faz query em XY
                var idx = SpatialGrid.HashPointToCell(pt, origin, step, iCount, jCount, kCount);
                var cellData = cells[idx];
                var queryEnv = new Envelope(pt.X, pt.X, pt.Y, pt.Y);
                var candidateBBoxes = cellData.RTree.Query(queryEnv);

                bool isInside = false;
                foreach (var bb in candidateBBoxes)
                {
                    if (!bboxToSolid.TryGetValue(bb, out var solid)) continue;

                    // b) teste ponto–dentro–da–malha
                    var mesh = solidMeshes[solid];
                    var bvh = meshBVH[solid];
                    if (IsPointInsideMesh(mesh, bvh, pt))
                    {
                        isInside = true;
                        break;
                    }
                }

                if (!isInside)
                    freePoints.Add(pt);
            }
            return freePoints;
        }

        private static bool IsPointInsideMesh(
            TriangulatedSolidOrShell mesh,
            AABBNode bvh,
            XYZ pt)
        {
            // Direção +Z
            var dir = XYZ.BasisZ;
            var ray = new Ray(pt, dir);

            int count = 0;
            TraverseBVH(bvh, mesh, ray, ref count);
            return (count % 2) == 1;                                                      // :contentReference[oaicite:5]{index=5}
        }

        // Recursão BVH: poda por Envelope antes de testar triângulos
        private static void TraverseBVH(AABBNode? node, TriangulatedSolidOrShell mesh, Ray ray, ref int count)
        {
            if (node == null)
                return;

            if (!RayIntersectsAABB(ray, node.Bounds)) return;

            if (node.IsLeaf)
            {
                foreach (var tri in node.Triangles!)
                {
                    if (RayTriangleIntersect(ray, tri, out _))
                        count++;
                }
            }
            else
            {
                if (node.Left != null) TraverseBVH(node.Left, mesh, ray, ref count);
                if (node.Right != null) TraverseBVH(node.Right, mesh, ray, ref count);
            }
        }

        private static bool RayIntersectsAABB(Ray ray, BoundingBoxXYZ bb)
        {
            // Usa slab method em cada eixo
            double tmin = (bb.Min.X - ray.Origin.X) / ray.Direction.X;
            double tmax = (bb.Max.X - ray.Origin.X) / ray.Direction.X;
            if (tmin > tmax) (tmin, tmax) = (tmax, tmin);

            double tymin = (bb.Min.Y - ray.Origin.Y) / ray.Direction.Y;
            double tymax = (bb.Max.Y - ray.Origin.Y) / ray.Direction.Y;
            if (tymin > tymax) (tymin, tymax) = (tymax, tymin);

            if ((tmin > tymax) || (tymin > tmax)) return false;
            if (tymin > tmin) tmin = tymin;
            if (tymax < tmax) tmax = tymax;

            double tzmin = (bb.Min.Z - ray.Origin.Z) / ray.Direction.Z;
            double tzmax = (bb.Max.Z - ray.Origin.Z) / ray.Direction.Z;
            if (tzmin > tzmax) (tzmin, tzmax) = (tzmax, tzmin);

            if ((tmin > tzmax) || (tzmin > tmax)) return false;
            return true;
        }

        // Möller–Trumbore: retorna true se o raio intersecta o triângulo
        private static bool RayTriangleIntersect(Ray ray, (XYZ V0, XYZ V1, XYZ V2) tri, out double t)
        {
            const double EPS = 1e-9;
            var v0 = tri.V0; var v1 = tri.V1; var v2 = tri.V2;
            var edge1 = v1 - v0;
            var edge2 = v2 - v0;
            var pvec = ray.Direction.CrossProduct(edge2);
            var det = edge1.DotProduct(pvec);
            if (Math.Abs(det) < EPS) { t = 0; return false; }
            var invDet = 1.0 / det;
            var tvec = ray.Origin - v0;
            var u = tvec.DotProduct(pvec) * invDet;
            if (u < 0 || u > 1) { t = 0; return false; }
            var qvec = tvec.CrossProduct(edge1);
            var v = ray.Direction.DotProduct(qvec) * invDet;
            if (v < 0 || u + v > 1) { t = 0; return false; }
            t = edge2.DotProduct(qvec) * invDet;
            return (t > EPS);
        }
    }

    // Representação minimalista de um raio
    internal struct Ray
    {
        public XYZ Origin, Direction;
        public Ray(XYZ origin, XYZ dir) { Origin = origin; Direction = dir.Normalize(); }
    }

    // Nó de BVH por AABB
    internal class AABBNode
    {
        public BoundingBoxXYZ Bounds { get; private set; }
        public AABBNode? Left, Right;
        public List<(XYZ V0, XYZ V1, XYZ V2)>? Triangles;
        public bool IsLeaf => Left == null && Right == null;

        private const int LeafTriangleThreshold = 8;  // Ajuste conforme necessário

        public AABBNode()
        {
            // 4) inicializa campos para evitar CS8618
            Bounds = new BoundingBoxXYZ();
            Triangles = new List<(XYZ, XYZ, XYZ)>();

        }

        /// Constrói um BVH recursivo a partir da malha triangulada.
        public static AABBNode Build(TriangulatedSolidOrShell mesh)
        {
            // 1) Extraímos todos os triângulos (XYZ triplets) da tesselação
            var allTris = new List<(XYZ, XYZ, XYZ)>();
            for (int ci = 0; ci < mesh.ShellComponentCount; ci++)
            {
                var comp = mesh.GetShellComponent(ci);
                var verts = comp.GetVertices();        // IList<XYZ>
                int triCount = comp.TriangleCount;
                for (int t = 0; t < triCount; t++)
                {
                    var tri = comp.GetTriangle(t);
                    var v0 = verts[tri.VertexIndex0];
                    var v1 = verts[tri.VertexIndex1];
                    var v2 = verts[tri.VertexIndex2];
                    allTris.Add((v0, v1, v2));
                }
            }
            // 3) garante que sempre retorna
            return BuildRecursive(allTris);
        }
        private static AABBNode BuildRecursive(List<(XYZ V0, XYZ V1, XYZ V2)> tris)
        {
            // 1) Cria nó e calcula bounds de todos os triângulos no conjunto
            var node = new AABBNode
            {
                Triangles = new List<(XYZ, XYZ, XYZ)>(tris),
                Bounds = ComputeBounds(tris)
            };

            // 2) Critério de leaf
            if (tris.Count <= LeafTriangleThreshold)
                return node;

            // 3) Determina eixo de maior extensão
            double dx = node.Bounds.Max.X - node.Bounds.Min.X;
            double dy = node.Bounds.Max.Y - node.Bounds.Min.Y;
            double dz = node.Bounds.Max.Z - node.Bounds.Min.Z;
            int axis = dx > dy
                ? (dx > dz ? 0 : 2)
                : (dy > dz ? 1 : 2);

            // 4) Calcula centroides e ordena
            tris.Sort((a, b) =>
            {
                var ca = ComputeCentroid(a);
                var cb = ComputeCentroid(b);
                return axis == 0
                    ? ca.X.CompareTo(cb.X)
                    : axis == 1
                        ? ca.Y.CompareTo(cb.Y)
                        : ca.Z.CompareTo(cb.Z);
            });

            int mid = tris.Count / 2;
            var left = tris.GetRange(0, mid);
            var right = tris.GetRange(mid, tris.Count - mid);

            // 5) Recursão
            node.Left = BuildRecursive(left);
            node.Right = BuildRecursive(right);
            node.Triangles.Clear();  // limpar no nó interno
            return node;
        }

        private static BoundingBoxXYZ ComputeBounds(List<(XYZ V0, XYZ V1, XYZ V2)> tris)
        {
            var bb = new BoundingBoxXYZ
            {
                Min = new XYZ(double.MaxValue, double.MaxValue, double.MaxValue),
                Max = new XYZ(double.MinValue, double.MinValue, double.MinValue)
            };

            foreach (var (v0, v1, v2) in tris)
            {
                foreach (XYZ v in new[] { v0, v1, v2 })
                {
                    bb.Min = new XYZ(
                        Math.Min(bb.Min.X, v.X),
                        Math.Min(bb.Min.Y, v.Y),
                        Math.Min(bb.Min.Z, v.Z));
                    bb.Max = new XYZ(
                        Math.Max(bb.Max.X, v.X),
                        Math.Max(bb.Max.Y, v.Y),
                        Math.Max(bb.Max.Z, v.Z));
                }
            }
            return bb;
        }

        private static XYZ ComputeCentroid((XYZ V0, XYZ V1, XYZ V2) tri)
        {
            return new XYZ(
                (tri.V0.X + tri.V1.X + tri.V2.X) / 3.0,
                (tri.V0.Y + tri.V1.Y + tri.V2.Y) / 3.0,
                (tri.V0.Z + tri.V1.Z + tri.V2.Z) / 3.0);
        }
    }
}