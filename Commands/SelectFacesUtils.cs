using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Linq;

namespace Commands.SelectFacesUtils
{
    public static class DadosSelecao
    {
        // Guarda referências das faces selecionadas
        public static List<Reference> ListaDeReferenciasDeFaces { get; private set; } = new List<Reference>();
    }

    [Transaction(TransactionMode.ReadOnly)]
    public class SelectFacesTGMCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                DadosSelecao.ListaDeReferenciasDeFaces.Clear();

                // Remove destaques anteriores
                var tgm = uiDoc.GetTemporaryGraphicsManager();
                tgm.RemoveAllTemporaryGraphics();

                // Seleção múltipla de faces
                IList<Reference> pickedRefs = uiDoc.Selection
                    .PickObjects(ObjectType.Face, "Selecione uma ou mais faces e clique em 'Finish'");

                foreach (var pickedRef in pickedRefs)
                {
                    var elem = doc.GetElement(pickedRef.ElementId);
                    var face = elem?.GetGeometryObjectFromReference(pickedRef) as PlanarFace;
                    if (face != null)
                    {
                        DadosSelecao.ListaDeReferenciasDeFaces.Add(pickedRef);
                    }
                }

                TaskDialog.Show("Seleção de Faces",
                    $"{DadosSelecao.ListaDeReferenciasDeFaces.Count} face(s) plana(s) armazenada(s).");

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                TaskDialog.Show("Cancelado", "A seleção de faces foi cancelada.");
                return Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.ReadOnly)]
    public class DestacarFacesComTGMCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = uiDoc.ActiveView;

            if (!DadosSelecao.ListaDeReferenciasDeFaces.Any())
            {
                TaskDialog.Show("Destacar Faces", "Nenhuma face armazenada para destacar.");
                return Result.Cancelled;
            }

            var tgm = uiDoc.GetTemporaryGraphicsManager();
            tgm.RemoveAllTemporaryGraphics();

            // Configurações gráficas
            var ogs = new OverrideGraphicSettings()
                .SetProjectionLineColor(new Color(255, 0, 0))
                .SetProjectionLineWeight(5);

            // Busca padrão sólido para preenchimento
            var solidFill = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

            if (solidFill != null)
            {
                ogs.SetSurfaceForegroundPatternId(solidFill.Id)
                   .SetSurfaceForegroundPatternColor(new Color(255, 255, 0));
            }

            int contFaces = 0;
            foreach (var faceRef in DadosSelecao.ListaDeReferenciasDeFaces)
            {
                var elem = doc.GetElement(faceRef.ElementId);
                var planarFace = elem?.GetGeometryObjectFromReference(faceRef) as PlanarFace;
                if (planarFace == null) continue;

                // Desenha contornos de cada loop
                foreach (var loop in planarFace.GetEdgesAsCurveLoops())
                {
                    // Adiciona curvas ao TGM
                    tgm.AddCurves(activeView, loop, ogs);
                }
                contFaces++;
            }

            uiDoc.RefreshActiveView();
            TaskDialog.Show("Destaque Visual", $"{contFaces} face(s) destacada(s). ");
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.ReadOnly)]
    public class LimparDestaqueTGMCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            var tgm = uiDoc.GetTemporaryGraphicsManager();
            tgm.RemoveAllTemporaryGraphics();
            DadosSelecao.ListaDeReferenciasDeFaces.Clear();
            uiDoc.RefreshActiveView();
            TaskDialog.Show("Limpar Destaque", "Destaque visual temporário removido.");
            return Result.Succeeded;
        }
    }
}
