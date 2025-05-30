using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Commands.Config
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ConfigCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            // Exibe a janela de configurações
            var dlg = new Views.Windows.ConfigWindow();
            dlg.ShowDialog();

            // Aqui você pode ler dlg.EPWPath, dlg.Season, dlg.StartDate, dlg.EndDate, dlg.MeshResolution
            return Result.Succeeded;
        }
    }
}
