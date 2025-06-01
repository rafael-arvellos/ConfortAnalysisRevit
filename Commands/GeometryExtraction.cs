using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

public static class GeometryUtils
{
    public static void ComputeBboxUV(
        PlanarFace face,
        out XYZ origin,
        out BoundingBoxUV bboxUV,
        out XYZ xVector,
        out XYZ yVector,
        out XYZ normal)
    {
        // Origem do plano
        origin = face.Origin;                     // :contentReference[oaicite:5]{index=5}
        // Vetores de base U e V
        xVector = face.XVector;                   // :contentReference[oaicite:6]{index=6}
        yVector = face.YVector;                   // :contentReference[oaicite:7]{index=7}
        // Vetor normal ao plano
        normal = face.FaceNormal;                 // :contentReference[oaicite:8]{index=8}
        // Bounding box em UV
        bboxUV = face.GetBoundingBox();           // :contentReference[oaicite:9]{index=9}
    }

    public static void ExtractGeometry(
        Document doc,
        ElementId elementId,
        out Dictionary<Solid, BoundingBoxXYZ> solidBBoxes,
        out BoundingBoxXYZ bbox)
    {
        // Recupera o elemento a partir do ID
        Element element = doc.GetElement(elementId)
            ?? throw new ArgumentException($"Elemento {elementId} não encontrado.");

        // Configurações para extrair geometria de modelo completo
        var geomOptions = new Options
        {
            ComputeReferences = false,
            DetailLevel = ViewDetailLevel.Fine,
            View = null
        };

        // Obtém o GeometryElement principal
        GeometryElement geomElem = element.get_Geometry(geomOptions);

        solidBBoxes = new Dictionary<Solid, BoundingBoxXYZ>();

        // Itera sobre cada objeto de geometria
        foreach (GeometryObject geomObj in geomElem)
        {
            // Se for Solid e contiver faces, adiciona diretamente
            if (geomObj is Solid s && s.Faces.Size > 0)
            {
                var bb = s.GetBoundingBox()
                         ?? throw new InvalidOperationException("Solid sem bbox");
                solidBBoxes[s] = bb;        
            }
            // Se for uma instância (como FamilyInstance), mergulha na geometria interna
            else if (geomObj is GeometryInstance geomInst)
            {
                foreach (GeometryObject instObj in geomInst.GetInstanceGeometry())
                {
                    if (instObj is Solid si && si.Faces.Size > 0)
                    {
                        var bb = si.GetBoundingBox()
                                ?? throw new InvalidOperationException("Solid sem bbox");
                        solidBBoxes[si] = bb;
                    }
                }
            }
        }

        // Obtém o bounding box do elemento no espaço do modelo
        bbox = element.get_BoundingBox(null)
            ?? throw new InvalidOperationException($"Elemento {elementId} não tem BoundingBox.");
    }

    public static List<XYZ> GenerateUVPoints(
        PlanarFace planarFace,
        double step)
    {
        // 1. Obter os parâmetros geométricos da face
        ComputeBboxUV(planarFace, 
                      out XYZ origin, 
                      out BoundingBoxUV uvBox, 
                      out XYZ xVec, 
                      out XYZ yVec, 
                      out XYZ normal);

        // 2. Calcula largura e altura no domínio UV
        double uMin = uvBox.Min.U, vMin = uvBox.Min.V;
        double uMax = uvBox.Max.U, vMax = uvBox.Max.V;

        double width  = uMax - uMin;
        double height = vMax - vMin;

        const double ep = 1e-6;

        // 3. Quantidade de passos em U e V
        int stepsU = (int)Math.Ceiling(width  / step);
        int stepsV = (int)Math.Ceiling(height / step);

        var points = new List<XYZ>();

        // 4. Loop sobre a malha UV
        for (int u = 0; u <= stepsU; u++)
        {
            double uRaw = uMin + u * step;
            double uParam = (uRaw < uMax)
                            ? uRaw
                            : (uMax - ep);

            if (uParam < uMin + ep)
                uParam = uMin = ep;            

            for (int v = 0; v <= stepsV; v++)
                {
                    double vRaw = vMin + v * step;
                    double vParam = (vRaw < vMax)
                                    ? vRaw
                                    : (vMax - ep);

                    if (vParam < vMin + ep)
                        vParam = vMin = ep;       
                             
                    //5. Verificar se o ponto uv está dentro da face
                    UV uvCoords = new UV(uParam, vParam);
                    if (!planarFace.IsInside(uvCoords))
                        continue;

                    XYZ pOnFace = planarFace.Evaluate(uvCoords);

                const double offset = 0.02;

                    // 7. Desloca 2 cm ao longo da normal
                    XYZ pOffset = pOnFace + normal.Multiply(offset);

                    points.Add(pOffset);
                }
        }
        return points;
    }
}