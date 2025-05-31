using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Commands.SelectionUtils;
using SpatialAnalysis;
using NetTopologySuite.Geometries;
using System;                    
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Commands.GeneratePoints
{
    
    public static class generatedPointsData
    {

        /// Lista estática para armazenar os pontos XYZ gerados.
        public static List<XYZ> pointList { get; private set; } = new List<XYZ>();

        /// Limpa todos os pontos armazenados na lista.
        public static void clearPointList()
        {
            pointList.Clear();
        }

        /// Adiciona uma coleção de pontos XYZ à lista.
        public static void addPoints(IEnumerable<XYZ> points)
        {
            if (points != null)
            {
                pointList.AddRange(points);
            }
        }

        /// Adiciona um único ponto XYZ à lista.
        public static void addPoint(XYZ point)
        {
            if (point != null)
            {
                pointList.Add(point);
            }
        }
    }

    [Transaction(TransactionMode.ReadOnly)]
    public class GenerateAndDisplaySurfacePointsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;


            // Verifica se existem faces selecionadas
            if (selectedPlanarFaceData.selectedPlanarFacesRefs == null || !selectedPlanarFaceData.selectedPlanarFacesRefs.Any())
            {
                Autodesk.Revit.UI.TaskDialog.Show("Gerar Pontos", "Nenhuma face armazenada. Use o comando 'Select Faces' primeiro.");
                return Result.Cancelled;
            }

            // Limpa a lista de pontos de execuções anteriores
            generatedPointsData.clearPointList();

            // Parâmetros para geração de pontos
            double pointStep = 0.5; // Espaçamento dos pontos na grade (em pés, unidade interna do Revit)

            int totalPointsStored = 0;

            try
            {
                foreach (var faceRef in selectedPlanarFaceData.selectedPlanarFacesRefs)
                {
                    Element? elem = doc.GetElement(faceRef.ElementId);
                    PlanarFace? planarFace = elem?.GetGeometryObjectFromReference(faceRef) as PlanarFace;

                    if (planarFace == null) continue;

                    // 1. Extrair dados da geometria da face
                    GeometryUtils.ComputeBboxUV(
                        planarFace,
                        out XYZ origin,
                        out BoundingBoxUV bboxUV,
                        out XYZ xVector,
                        out XYZ yVector,
                        out XYZ normal);

                    // 2. Gerar pontos na superfície da face
                    List<XYZ> surfacePoints = GeometryUtils.GenerateUVPoints(
                        planarFace,
                        pointStep);

                    //PointFilter

                    // 3. Armazenar os pontos gerados
                    if (surfacePoints != null && surfacePoints.Any())
                    {
                        generatedPointsData.addPoints(surfacePoints);
                        totalPointsStored += surfacePoints.Count;
                    }
                }

                Autodesk.Revit.UI.TaskDialog.Show("Pontos Gerados", $"{totalPointsStored} ponto(s) foram gerado(s) e armazenado(s) na lista interna.");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"Erro ao gerar e armazenar pontos: {ex.Message}";
                return Result.Failed;
            }
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
            intersectionCommands.TraverseBVH(bvh, mesh, ray, ref count);
            return (count % 2) == 1;                                                      // :contentReference[oaicite:5]{index=5}
        }
    }






    [Transaction(TransactionMode.ReadOnly)]
    public class ExportFacePointsToSvgCommand : IExternalCommand
    {
        // Constantes para a geração do SVG
        private const double SvgScale = 100.0; // Pixels por unidade UV do Revit (ex: 100 pixels por pé)
        private const double PointRadiusSvg = 2.0; // Raio dos círculos no SVG (em pixels)
        private const string PointFillColorSvg = "blue"; // Cor dos pontos no SVG
        private const string SvgBackgroundColor = "lightgray";
        private const string EdgeStrokeColorSvg = "black";
        private const double EdgeStrokeWidthSvg = 1.0; // Em pixels

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            // 1. Verificar se existem faces selecionadas
            if (selectedPlanarFaceData.selectedPlanarFacesRefs == null || !selectedPlanarFaceData.selectedPlanarFacesRefs.Any())
            {
                Autodesk.Revit.UI.TaskDialog.Show("Exportar SVG", "Nenhuma face armazenada. Use o comando 'Select Faces' primeiro.");
                return Result.Cancelled;
            }

            // 2. Pedir ao usuário para selecionar uma pasta
            string? outputFolderPath = null;
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Selecione a pasta para salvar os arquivos SVG";
                folderDialog.ShowNewFolderButton = true;
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    outputFolderPath = folderDialog.SelectedPath;
                }
            }

            if (string.IsNullOrEmpty(outputFolderPath))
            {
                Autodesk.Revit.UI.TaskDialog.Show("Exportar SVG", "Nenhuma pasta selecionada. Operação cancelada.");
                return Result.Cancelled;
            }

            int svgFileCounter = 0;
            int faceProcessedCounter = 0;

            try
            {
                // 3. Iterar sobre cada face selecionada
                foreach (var faceRef in selectedPlanarFaceData.selectedPlanarFacesRefs)
                {
                    faceProcessedCounter++;
                    Element? elem = doc.GetElement(faceRef.ElementId);
                    PlanarFace? planarFace = elem?.GetGeometryObjectFromReference(faceRef) as PlanarFace;

                    if (elem == null || planarFace == null)
                    {
                        // Log ou informa que uma face não pôde ser processada
                        System.Diagnostics.Debug.WriteLine($"Aviso: Não foi possível processar a face {faceProcessedCounter} (Elemento: {faceRef.ElementId}).");
                        continue;
                    }

                    // 4.a. Obter dados geométricos da face
                    GeometryUtils.ComputeBboxUV(
                        planarFace,
                        out XYZ origin,
                        out BoundingBoxUV bboxUV,
                        out XYZ xVec,
                        out XYZ yVec,
                        out XYZ normal);

                    // 4.b. Calcular dimensões do SVG
                    double uvWidth = bboxUV.Max.U - bboxUV.Min.U;
                    double uvHeight = bboxUV.Max.V - bboxUV.Min.V;

                    double svgWidth = uvWidth * SvgScale;
                    double svgHeight = uvHeight * SvgScale;

                    // Evitar SVGs com dimensão zero ou negativa se o BBox for inválido
                    if (svgWidth <= 0 || svgHeight <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Aviso: Dimensões UV inválidas para a face {faceProcessedCounter} (Elemento: {elem.Id}). SVG não gerado.");
                        continue;
                    }

                    // 4.c. Iniciar construção da string SVG
                    StringBuilder svgBuilder = new StringBuilder();
                    svgBuilder.AppendLine($"<svg width='{svgWidth:F2}' height='{svgHeight:F2}' xmlns='http://www.w3.org/2000/svg' style='background-color:{SvgBackgroundColor}; border:1px solid black;'>");
                    // Adiciona um título ao SVG (opcional)
                    svgBuilder.AppendLine($"  <title>Face Points - Element {elem.Id} - Face {faceProcessedCounter}</title>");
                    
                    IList<CurveLoop> curveLoops = planarFace.GetEdgesAsCurveLoops();
                    foreach (CurveLoop loop in curveLoops)
                    {
                        StringBuilder polylinePointsStr = new StringBuilder();
                        foreach (Curve curveInLoop in loop)
                        {
                            IList<XYZ> tessellatedPoints = curveInLoop.Tessellate();
                            foreach (XYZ pointOnEdge3D in tessellatedPoints)
                            {
                                // Projetar o ponto 3D da aresta para o sistema UV 2D da face
                                XYZ vectorFromOrigin = pointOnEdge3D - origin; // 'origin' da face
                                double u_coord_face = vectorFromOrigin.DotProduct(xVec); // 'xVec' da face
                                double v_coord_face = vectorFromOrigin.DotProduct(yVec); // 'yVec' da face

                                // Converter coordenadas UV da face para coordenadas SVG
                                double svg_edge_x = (u_coord_face - bboxUV.Min.U) * SvgScale;
                                double svg_edge_y = (bboxUV.Max.V - v_coord_face) * SvgScale; // Y é invertido

                                polylinePointsStr.AppendFormat("{0:F2},{1:F2} ", svg_edge_x, svg_edge_y);
                            }
                        }

                        if (polylinePointsStr.Length > 0)
                        {
                            svgBuilder.AppendLine($"  <polyline points='{polylinePointsStr.ToString().TrimEnd()}' fill='none' stroke='{EdgeStrokeColorSvg}' stroke-width='{EdgeStrokeWidthSvg:F1}'/>");
                        }
                    }

                    // 4.d. Recriar lógica de geração de pontos para obter coordenadas UV relativas
                    double pointStep = 0.5; // Use o mesmo step da geração original, se aplicável, ou defina um aqui.
                                            // Este é o step usado em GeometryUtils.GenerateUVPoints

                    double uMin = bboxUV.Min.U;
                    double vMin = bboxUV.Min.V;
                    double uMax = bboxUV.Max.U;
                    double vMax = bboxUV.Max.V;

                    int stepsU = (int)Math.Ceiling(uvWidth / pointStep);
                    int stepsV = (int)Math.Ceiling(uvHeight / pointStep);

                    for (int u_idx = 0; u_idx <= stepsU; u_idx++)
                    {
                        double current_u_on_face = Math.Min(uMin + u_idx * pointStep, uMax);
                        for (int v_idx = 0; v_idx <= stepsV; v_idx++)
                        {
                            double current_v_on_face = Math.Min(vMin + v_idx * pointStep, vMax);

                            // 4.e. Transformar para coordenadas SVG
                            // cx: relativo à esquerda do bboxUV, escalado
                            double cx = (current_u_on_face - uMin) * SvgScale;
                            // cy: relativo ao topo do bboxUV (Y invertido), escalado
                            double cy = (vMax - current_v_on_face) * SvgScale;

                            svgBuilder.AppendLine($"  <circle cx='{cx:F2}' cy='{cy:F2}' r='{PointRadiusSvg:F2}' fill='{PointFillColorSvg}' />");
                        }
                    }

                    // 4.f. Fechar tag SVG
                    svgBuilder.AppendLine("</svg>");

                    // 4.g. Gerar nome do arquivo e salvar
                    // Usar um nome de arquivo mais robusto para evitar problemas com caracteres inválidos
                    string safeElementName = elem.Id.ToString();
                    // Se o elemento tiver um nome, pode ser mais descritivo, mas precisa sanitizar
                    // string elementNameSanitized = string.Join("_", (elem.Name ?? $"Elem_{elem.Id}").Split(Path.GetInvalidFileNameChars()));

                    string fileName = Path.Combine(outputFolderPath, $"FacePoints_Elem{safeElementName}_FaceIdx{faceProcessedCounter}.svg");
                    File.WriteAllText(fileName, svgBuilder.ToString());
                    svgFileCounter++;
                }

                Autodesk.Revit.UI.TaskDialog.Show("Exportar SVG", $"{svgFileCounter} arquivo(s) SVG gerado(s) com sucesso em:\n{outputFolderPath}");
            }
            catch (Exception ex)
            {
                message = $"Erro ao exportar SVGs: {ex.Message}\nStackTrace: {ex.StackTrace}";
                Autodesk.Revit.UI.TaskDialog.Show("Erro ao Exportar SVG", message);
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }

}