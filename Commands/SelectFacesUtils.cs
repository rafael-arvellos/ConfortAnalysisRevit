using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic; // Necessário para List<>

namespace Commands.SelectionUtils
{

    /// Classe estática para armazenar as PlanarFaces selecionadas pelo usuário.
    public static class DadosPlanarFace
    {
        /// Lista que conterá as faces planas selecionadas.
        public static List<PlanarFace> FacesPlanasSelecionadas { get; private set; } = new List<PlanarFace>();

        /// Método auxiliar para limpar a lista de faces selecionadas.
        public static void LimparFacesSelecionadas()
        {
            FacesPlanasSelecionadas.Clear();
        }

        /// Método auxiliar para adicionar uma face à lista.
        public static void AdicionarFace(PlanarFace face)
        {
            FacesPlanasSelecionadas.Add(face);
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
                DadosPlanarFace.LimparFacesSelecionadas();

                // 2. Solicita ao usuário que selecione faces.
                IList<Reference> pickedRefs = uiDoc.Selection.PickObjects(
                    ObjectType.Face,
                    "Selecione uma ou mais faces planas e clique em 'Finish' na barra de opções." // Mensagem mostrada ao usuário.
                );

                // Verifica se o usuário selecionou algo.
                if (pickedRefs == null || pickedRefs.Count == 0)
                {
                    TaskDialog.Show("Seleção", "Nenhuma face foi selecionada.");
                    return Result.Cancelled; // Usuário não selecionou nada ou cancelou.
                }

                int facesPlanasAdicionadas = 0;
                foreach (Reference pickedRef in pickedRefs)
                {
                    // 3. Para cada Reference obtida, precisamos pegar o objeto de geometria real.
                    Element elem = doc.GetElement(pickedRef.ElementId);

                    // Depois, usamos a Reference para obter o GeometryObject específico (a face).
                    GeometryObject geoObject = elem.GetGeometryObjectFromReference(pickedRef);

                    // 4. Verificamos se o objeto geométrico é de fato uma PlanarFace.
                    if (geoObject is PlanarFace planarFace)
                    {
                        // Se for uma PlanarFace, adicionamos à nossa lista estática.
                        DadosPlanarFace.AdicionarFace(planarFace);
                        facesPlanasAdicionadas++;
                    }
                }

                // 5. Informa ao usuário quantas faces planas foram armazenadas.
                TaskDialog.Show("Seleção de Faces Planas Concluída",
                    $"{facesPlanasAdicionadas} face(s) plana(s) foram selecionadas e armazenadas com sucesso.\n" +
                    $"Total na lista: {DadosPlanarFace.FacesPlanasSelecionadas.Count} face(s).");

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Ocorre se o usuário pressionar ESC durante a seleção.
                TaskDialog.Show("Cancelado", "A seleção de faces foi cancelada pelo usuário.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                // Captura qualquer outro erro inesperado.
                message = $"Erro ao selecionar faces planas: {ex.Message}";
                TaskDialog.Show("Erro", message);
                return Result.Failed;
            }
        }
    }


}