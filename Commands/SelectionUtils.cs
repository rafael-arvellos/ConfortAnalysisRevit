using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using ConfortAnalysis.Data;


namespace ConfortAnalysis.Commands
{
    [Transaction(TransactionMode.ReadOnly)] 
    [Regeneration(RegenerationOption.Manual)] 
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
                ApplicationDataFunctions.ClearRefs(ApplicationData.SelectedPlanarFacesRefs);

                // 2. Solicita ao usuário que selecione faces.
                IList<Reference> pickedRefs = uiDoc.Selection.PickObjects(
                    ObjectType.Face,
                    "Selecione uma ou mais faces planas e clique em 'Finish' na barra de opções." // Mensagem mostrada ao usuário.
                );
                
                if (pickedRefs != null && pickedRefs.Any())
                {
                    ApplicationDataFunctions.AddRefs(ApplicationData.SelectedPlanarFacesRefs, pickedRefs);

                    Autodesk.Revit.UI.TaskDialog.Show("Seleção de Faces", $"{ApplicationData.SelectedPlanarFacesRefs.Count} face(s) selecionado(s) e referência(s) armazenada(s).");
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

    [Transaction(TransactionMode.ReadOnly)]
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
                ApplicationDataFunctions.ClearRefs(ApplicationData.SelectedElementsRefs);

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
                    ApplicationDataFunctions.AddRefs(ApplicationData.SelectedElementsRefs, newReferences);

                    // 4. Feedback ao Usuário
                    Autodesk.Revit.UI.TaskDialog.Show("Seleção Capturada", $"{ApplicationData.SelectedElementsRefs.Count} elemento(s) selecionado(s) foram capturados e suas referências armazenadas.");
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