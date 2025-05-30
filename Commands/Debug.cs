using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System.Text; // Para StringBuilder
using Commands.SelectionUtils;

// Certifique-se que os namespaces de selectedPlanarFaceData e selectedElementData estão acessíveis
// Exemplo: using ConfortAnalysis.Commands;

namespace Commands.Debug
{
    [Transaction(TransactionMode.Manual)] // Precisamos de transação para SetElementOverrides
    public class DebugSelectionInfoCommand : IExternalCommand
    {
        // Para rastrear elementos que este comando modificou, para limpeza.
        // Poderia ser mais sofisticado, mas para debug, limpamos os da lista atual.
        // Se quiser um controle mais fino, precisaria armazenar ElementIds entre execuções.

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = uiDoc.ActiveView;

            if (activeView == null)
            {
                message = "Por favor, execute este comando em uma vista de projeto ativa.";
                return Result.Failed;
            }

            StringBuilder sb = new StringBuilder();

            // --- INÍCIO DA SEÇÃO DE LISTAGEM DE INFORMAÇÕES (Permanece Igual) ---
            sb.AppendLine("--- Faces Planas Selecionadas (selectedPlanarFaceData.selectedPlanarFacesRefs) ---");
            List<Reference> faceRefs = selectedPlanarFaceData.selectedPlanarFacesRefs;
            sb.AppendLine($"Quantidade: {faceRefs.Count}");
            if (faceRefs.Any())
            {
                for (int i = 0; i < faceRefs.Count; i++)
                {
                    Reference r = faceRefs[i];
                    sb.AppendLine($"  {i + 1}: ElemId={r.ElementId}, StableRepresentation='{r.ConvertToStableRepresentation(doc)}'");
                }
            }
            else
            {
                sb.AppendLine("  Nenhuma referência de face plana armazenada.");
            }
            sb.AppendLine();

            sb.AppendLine("--- Elementos Selecionados (selectedElementData.selectedElementsRefs) ---");
            List<Reference> elementRefsFromSelection = selectedElementData.selectedElementsRefs;
            sb.AppendLine($"Quantidade: {elementRefsFromSelection.Count}");
            if (elementRefsFromSelection.Any())
            {
                for (int i = 0; i < elementRefsFromSelection.Count; i++)
                {
                    Reference r = elementRefsFromSelection[i];
                    sb.AppendLine($"  {i + 1}: ElemId={r.ElementId}, StableRepresentation='{r.ConvertToStableRepresentation(doc)}'");
                }
            }
            else
            {
                sb.AppendLine("  Nenhuma referência de elemento armazenada.");
            }
            // --- FIM DA SEÇÃO DE LISTAGEM DE INFORMAÇÕES ---

            TaskDialog.Show("Debug Informações de Seleção", sb.ToString());

            // --- INÍCIO DA SEÇÃO DE DESTAQUE COM OVERRIDEGRAPHICSETTINGS ---
            using (Transaction tx = new Transaction(doc, "Aplicar Destaques de Debug"))
            {
                tx.Start();

                // Configurações gráficas para elementos que contêm faces selecionadas
                var ogsForFaceElements = new OverrideGraphicSettings();
                ogsForFaceElements.SetProjectionLineColor(new Color(255, 0, 0)); // Vermelho
                ogsForFaceElements.SetProjectionLineWeight(6);
                // Opcional: Preenchimento de superfície para os elementos das faces
                var solidFill = new FilteredElementCollector(doc)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
                if (solidFill != null)
                {
                    ogsForFaceElements.SetSurfaceForegroundPatternId(solidFill.Id);
                    ogsForFaceElements.SetSurfaceForegroundPatternColor(new Color(255, 165, 0)); // Laranja
                }

                // Configurações gráficas para elementos selecionados diretamente
                var ogsForSelectedElements = new OverrideGraphicSettings();
                ogsForSelectedElements.SetProjectionLineColor(new Color(0, 0, 255)); // Azul
                ogsForSelectedElements.SetProjectionLineWeight(6);
                if (solidFill != null) // Reutilizando o solidFill
                {
                    ogsForSelectedElements.SetSurfaceForegroundPatternId(solidFill.Id);
                    ogsForSelectedElements.SetSurfaceForegroundPatternColor(new Color(173, 216, 230)); // Azul claro
                }
                
                // Configurações para limpar overrides
                var clearOgs = new OverrideGraphicSettings(); // Um OGS vazio limpa os overrides existentes

                // Coletar todos os ElementIds únicos das duas listas para limpar primeiro
                // Isso garante que se um item foi removido da seleção desde a última vez,
                // seu destaque também será removido ao rodar o debug novamente.
                HashSet<ElementId> idsToClear = new HashSet<ElementId>();
                faceRefs.ForEach(r => idsToClear.Add(r.ElementId));
                elementRefsFromSelection.ForEach(r => idsToClear.Add(r.ElementId));
                
                foreach (ElementId id in idsToClear)
                {
                    activeView.SetElementOverrides(id, clearOgs);
                }

                // Aplicar Destaque para Elementos que contêm Faces Planas
                if (faceRefs.Any())
                {
                    // Obter ElementIds únicos, pois várias faces podem pertencer ao mesmo elemento
                    List<ElementId> uniqueFaceElementIds = faceRefs.Select(r => r.ElementId).Distinct().ToList();
                    foreach (ElementId elemId in uniqueFaceElementIds)
                    {
                        activeView.SetElementOverrides(elemId, ogsForFaceElements);
                    }
                    sb.AppendLine($"\n{uniqueFaceElementIds.Count} elemento(s) pai(s) de faces destacadas em Vermelho/Laranja.");
                }

                // Aplicar Destaque para Elementos Selecionados Diretamente
                if (elementRefsFromSelection.Any())
                {
                    List<ElementId> uniqueSelectedElementIds = elementRefsFromSelection.Select(r => r.ElementId).Distinct().ToList();
                    foreach (ElementId elemId in uniqueSelectedElementIds)
                    {
                        // Evitar aplicar duas vezes se um elemento estiver em ambas as lógicas de destaque
                        // (ex: elemento selecionado diretamente também é pai de uma face selecionada).
                        // A última aplicação de override prevalecerá.
                        // Se quisermos cores diferentes, podemos ter uma lógica mais complexa.
                        // Por ora, se um elemento é "pai de face" e "selecionado", o override de elemento selecionado (azul) pode sobrescrever o de face.
                        // Ou podemos priorizar: se for pai de face, fica vermelho, senão, se for selecionado, fica azul.
                        // Para simplificar, vamos aplicar. A cor que "vence" dependerá da ordem ou de lógica adicional.
                        // Uma forma de dar prioridade:
                        if (!faceRefs.Any(fr => fr.ElementId == elemId)) // Só aplica azul se não foi destacado como vermelho
                        {
                           activeView.SetElementOverrides(elemId, ogsForSelectedElements);
                        } else {
                            // Se já foi destacado como vermelho (pai de face), podemos decidir não aplicar o azul
                            // ou aplicar uma cor combinada (mais complexo).
                            // Por ora, o override vermelho (se aplicado) já está lá.
                            // Se quisermos que o azul seja aplicado mesmo assim (sobrepondo):
                            // activeView.SetElementOverrides(elemId, ogsForSelectedElements);
                        }
                    }
                    sb.AppendLine($"\n{uniqueSelectedElementIds.Count} elemento(s) selecionados diretamente destacados em Azul/Azul Claro (se não forem pais de faces).");
                }

                tx.Commit();
            }
            // --- FIM DA SEÇÃO DE DESTAQUE ---
            
            // A mensagem do TaskDialog foi mostrada antes da transação
            // Se quiser mostrar uma mensagem de resumo APÓS os destaques:
            if (faceRefs.Any() || elementRefsFromSelection.Any()) {
                 TaskDialog.Show("Destaque com OverrideGraphics", "Destaques aplicados à vista atual.\n" +
                                                                 "Lembre-se que estes overrides persistem.\n" +
                                                                 "Execute o comando novamente com seleções vazias ou use um comando de 'Limpar Destaques' para removê-los.");
            }


            return Result.Succeeded;
        }
    }
}