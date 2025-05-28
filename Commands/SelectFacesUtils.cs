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

    /// Comando para destacar visualmente as PlanarFaces armazenadas na lista estática
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class DestacarPlanarFacesCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = uiDoc.ActiveView; // vista ativa

            // Verifica se a lista de faces está disponível e contém faces
            if (DadosPlanarFace.FacesPlanasSelecionadas == null || !DadosPlanarFace.FacesPlanasSelecionadas.Any())
            {
                TaskDialog.Show("Destacar Faces Planas", "Nenhuma face plana armazenada na lista para destacar.");
                return Result.Cancelled;
            }

            // Obtém o Temporary Graphics Manager
            var tgm = TemporaryGraphicsManager.GetTemporaryGraphicsManager(doc);
            if (tgm == null)
            {
                message = "Não foi possível obter o Temporary Graphics Manager.";
                TaskDialog.Show("Erro", message);
                return Result.Failed;
            }

            // Limpa quaisquer gráficos temporários anteriores para evitar sobreposição
            tgm.Clear();

            // Configurações gráficas para o destaque (semelhante ao seu DestacarFacesComTGMCommand)
            var ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(new Color(0, 0, 255)); // Linhas de contorno em Azul
            ogs.SetProjectionLineWeight(6); // Espessura da linha um pouco maior para destaque

            // Tenta aplicar um padrão de preenchimento à superfície
            // Buscamos um padrão de preenchimento sólido existente no projeto.
            var solidFillPattern = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);

            if (solidFillPattern != null)
            {
                ogs.SetSurfaceForegroundPatternId(solidFillPattern.Id);
                ogs.SetSurfaceForegroundPatternColor(new Color(255, 255, 0)); // Preenchimento em Amarelo (se aplicado pelo AddCurves)
            }
            else
            {
                // Informa o usuário se o padrão sólido não for encontrado, o destaque será apenas de contorno.
                TaskDialog.Show("Aviso de Destaque", "Padrão de preenchimento sólido não encontrado no projeto. As faces serão destacadas apenas com contornos coloridos.");
            }

            int facesDestacadas = 0;
            // Itera sobre cada PlanarFace armazenada na nossa lista
            foreach (PlanarFace planarFace in DadosPlanarFace.FacesPlanasSelecionadas)
            {
                // Verifica se o objeto PlanarFace ainda é válido (pode ter sido invalidado por mudanças no modelo)
                if (planarFace == null)
                {
                    // Poderíamos registrar um aviso aqui, se desejado.
                    continue;
                }

                // Obtém os loops de arestas (contornos) da face.
                // Uma face pode ter um contorno externo e múltiplos contornos internos (furos).
                IList<CurveLoop> edgeLoops = planarFace.GetEdgesAsCurveLoops();

                foreach (CurveLoop loop in edgeLoops)
                {
                    // Adiciona as curvas do loop ao TGM com as configurações gráficas definidas.
                    // Analogia: Estamos "desenhando" o contorno da face na vista.
                    tgm.AddCurves(activeView, loop, ogs);
                }
                facesDestacadas++;
            }

            // Atualiza a vista ativa para que os gráficos temporários sejam exibidos.
            uiDoc.RefreshActiveView();

            TaskDialog.Show("Destaque Visual", $"{facesDestacadas} face(s) plana(s) foram destacada(s) na vista atual.");
            return Result.Succeeded;
        }
    }

    /// <summary>
    /// Comando para limpar todos os destaques visuais criados pelo TemporaryGraphicsManager
    /// que foram usados para destacar as PlanarFaces.
    /// </summary>
    [Transaction(TransactionMode.ReadOnly)]
    [Regeneration(RegenerationOption.Manual)]
    public class LimparDestaquePlanarFacesCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            var tgm = uiDoc.GetTemporaryGraphicsManager();
            if (tgm == null)
            {
                message = "Não foi possível obter o Temporary Graphics Manager.";
                TaskDialog.Show("Erro", message);
                return Result.Failed;
            }
            
            // Remove todos os gráficos temporários da vista.
            // Analogia: Apaga todos os desenhos temporários da tela.
            tgm.RemoveAllTemporaryGraphics();
            
            // Opcional: Você pode querer limpar a lista de faces armazenadas aqui também,
            // dependendo do workflow desejado. Por exemplo:
            // DadosPlanarFace.LimparFacesSelecionadas(); 
            // Por agora, o comando apenas limpa o destaque visual.

            uiDoc.RefreshActiveView(); // Atualiza a vista para refletir a remoção dos destaques.
            TaskDialog.Show("Limpar Destaque", "Destaque visual temporário das faces planas foi removido.");
            return Result.Succeeded;
        }
    }

}