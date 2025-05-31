using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;


namespace Commands.SelectionUtils
{

    /// Classe estática para armazenar as PlanarFaces selecionadas pelo usuário.
    public static class selectedPlanarFaceData
    {
        /// Lista que conterá as faces planas selecionadas.
        public static List<Reference> selectedPlanarFacesRefs { get; private set; } = new List<Reference>();

        /// Método auxiliar para limpar a lista de faces selecionadas.
        public static void clearPlanarFacesRefs()
        {
            selectedPlanarFacesRefs.Clear();
        }

        /// Método auxiliar para adicionar uma face à lista.
        public static void addPlanarFaceRefs(IList<Reference> reference)
        {
            selectedPlanarFacesRefs.AddRange(reference);
        }

        // Se você quiser um método para obter as PlanarFaces na hora (com o Document atual)
        public static List<PlanarFace> getSelectedPlanarFaces(Document doc)
        {
            var planarFaces = new List<PlanarFace>();
            foreach (var faceRef in selectedPlanarFacesRefs)
            {
                Element elem = doc.GetElement(faceRef.ElementId);
                if (elem != null)
                {
                    GeometryObject geoObj = elem.GetGeometryObjectFromReference(faceRef);
                    if (geoObj is PlanarFace pf)
                    {
                        planarFaces.Add(pf);
                    }
                }
            }
            return planarFaces;
        }
    }

    /// Classe estática para armazenar os Elementos selecionadas pelo usuário.
    public static class selectedElementData
    {

        /// Lista que conterá os elementos selecionadas.
        public static List<Reference> selectedElementsRefs { get; private set; } = new List<Reference>();

        /// Método auxiliar para limpar a lista de elementos selecionados.
        public static void clearSelectedElementRefs()
        {
            selectedElementsRefs.Clear();
        }

        /// Método auxiliar para limpar a lista de elementos selecionados.
        public static void addElementRefs(IList<Reference> references)
        {
            selectedElementsRefs.AddRange(references);
        }
    }

    /// Comando externo para selecionar uma ou mais faces planas (PlanarFace)
    [Transaction(TransactionMode.ReadOnly)] // ReadOnly porque apenas lemos dados do modelo.
    [Regeneration(RegenerationOption.Manual)] // Não precisamos de regeneração automática.
    public class SelectFacesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // UIApplication nos dá acesso à interface do usuário e ao documento ativo.
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // 1. Limpa a lista de faces anteriormente selecionadas.
                selectedPlanarFaceData.clearPlanarFacesRefs();

                // 2. Solicita ao usuário que selecione faces.
                IList<Reference> pickedRefs = uiDoc.Selection.PickObjects(
                    ObjectType.Face,
                    "Selecione uma ou mais faces planas e clique em 'Finish' na barra de opções." // Mensagem mostrada ao usuário.
                );
                
                if (pickedRefs != null && pickedRefs.Any())
                {
                    selectedPlanarFaceData.addPlanarFaceRefs(pickedRefs);

                    Autodesk.Revit.UI.TaskDialog.Show("Seleção de Faces", $"{selectedPlanarFaceData.selectedPlanarFacesRefs.Count} face(s) selecionado(s) e referência(s) armazenada(s).");
                }
                else
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Seleção", "Nenhuma face foi selecionada.");
                    return Result.Cancelled; // Usuário não selecionou nada ou cancelou.
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Ocorre se o usuário pressionar ESC durante a seleção.
                Autodesk.Revit.UI.TaskDialog.Show("Cancelado", "A seleção de faces foi cancelada pelo usuário.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                // Captura qualquer outro erro inesperado.
                message = $"Erro ao selecionar faces planas: {ex.Message}";
                Autodesk.Revit.UI.TaskDialog.Show("Erro", message);
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.ReadOnly)] // ReadOnly porque apenas lemos dados do modelo.
    public class CaptureCurrentSelectionCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // 1. Limpa a lista de faces anteriormente selecionadas.
                selectedElementData.clearSelectedElementRefs();

                // 2. Obter os ElementIds da seleção atual na UI do Revit
                ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();

                if (selectedIds != null && selectedIds.Any())
                {
                    // 3. Converter ElementIds em References e adicioná-los à lista
                    List<Reference> newReferences = new List<Reference>();
                    foreach (ElementId id in selectedIds)
                    {
                        Element elem = doc.GetElement(id);
                        if (elem != null)
                        {
                            Reference elemRef = new Reference(elem);
                            newReferences.Add(elemRef);
                        }
                    }

                    // Adiciona as novas referências à classe de armazenamento estático
                    selectedElementData.addElementRefs(newReferences);

                    // 4. Feedback ao Usuário
                    Autodesk.Revit.UI.TaskDialog.Show("Seleção Capturada", $"{selectedElementData.selectedElementsRefs.Count} elemento(s) selecionado(s) foram capturados e suas referências armazenadas.");
                }
                else
                {
                    Autodesk.Revit.UI.TaskDialog.Show("Capturar Seleção", "Nenhum elemento está selecionado no Revit.");
                    return Result.Cancelled;
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Autodesk.Revit.UI.TaskDialog.Show("Cancelado", "A seleção de elementos foi cancelada.");
                return Result.Cancelled;
            }
            catch (System.Exception ex)
            {
                message = ex.Message;
                Autodesk.Revit.UI.TaskDialog.Show("Erro na Seleção", $"Ocorreu um erro: {ex.Message}");
                return Result.Failed;
            }
        }
    }

}