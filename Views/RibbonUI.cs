using System;
using System.IO;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using System.Reflection;

namespace ConfortAnalysis.Views
{
    public class RibbonUI
    {
        public static void CreateRibbonUI(UIControlledApplication application)
        {
            // 1) Cria o tab, se ainda não existir
            string tabName = "Confort Plugin";
            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch { /* Tab já existe */}

            // 2) Cria o painel
            RibbonPanel panel = application.CreateRibbonPanel(tabName, "Direct Sun Hours");

            // 3) Caminho para a dll deste add-in
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            // 4) Botões principais
            AddPushButton(panel, "SelectFaces",    "Select Faces",    assemblyPath, "Commands.SelectFacesUtils.SelectFacesCommand",   "Resources/icons/select.png");
            AddSplitButtonsForCollection(panel,
                                         "Faces",
                                         "FaceIds",
                                         assemblyPath,
                                         addCmd:    "Commands.SelectFacesUtils.DestacarElementosCommand",
                                         removeCmd: "ConfortAnalysis.Commands.RemoveFaceIdCommand",
                                         addText:   "Add FaceId",
                                         removeText:"Remove FaceId",
                                         addIcon:   "Resources/icons/add.png",
                                         remIcon:   "Resources/icons/remove.png");

            AddPushButton(panel, "SelectGeoms",     "Select Geometries",    assemblyPath, "ConfortAnalysis.Commands.SelectGeometriesCommand",   "Resources/icons/select.png");
            AddSplitButtonsForCollection(panel,
                                         "Geoms",
                                         "GeometryIds",
                                         assemblyPath,
                                         addCmd:    "ConfortAnalysis.Commands.AddGeometryIdCommand",
                                         removeCmd: "ConfortAnalysis.Commands.RemoveGeometryIdCommand",
                                         addText:   "Add GeometryId",
                                         removeText:"Remove GeometryId",
                                         addIcon:   "Resources/icons/add.png",
                                         remIcon:   "Resources/icons/remove.png");

            AddPushButton(panel, "ConfigDirectSun", "Config.",       assemblyPath, "ConfortAnalysis.Commands.ConfigDirectSunHoursCommand", "Resources/icons/config.png");
            AddPushButton(panel, "RunDirectSun",    "Run",           assemblyPath, "ConfortAnalysis.Commands.RunDirectSunHoursCommand",    "Resources/icons/run.png");

        }
        
        private static void AddPushButton(RibbonPanel panel,
                                   string internalName,
                                   string buttonText,
                                   string assemblyPath,
                                   string className,
                                   string iconPath)
        {
            var buttonData = new PushButtonData(internalName, buttonText, assemblyPath, className)
            {
                ToolTip = buttonText,
                LargeImage = LoadImage(iconPath),
                Image      = LoadImage(iconPath)
            };
            panel.AddItem(buttonData);
        }

        private static void AddSplitButtonsForCollection(RibbonPanel panel,
                                                  string groupName,
                                                  string idListName,
                                                  string assemblyPath,
                                                  string addCmd,
                                                  string removeCmd,
                                                  string addText,
                                                  string removeText,
                                                  string addIcon,
                                                  string remIcon)
        {
            var split   = panel.AddStackedItems(
                new PushButtonData($"{groupName}_Add",    addText,    assemblyPath, addCmd),
                new PushButtonData($"{groupName}_Remove", removeText, assemblyPath, removeCmd)
            );

            // configura cada um
            ((PushButton) split[0]).LargeImage = LoadImage(addIcon);
            ((PushButton) split[0]).Image      = LoadImage(addIcon);
            ((PushButton) split[0]).ToolTip    = $"Adicionar {groupName}"; 
            ((PushButton) split[1]).LargeImage = LoadImage(remIcon);
            ((PushButton) split[1]).Image      = LoadImage(remIcon);
            ((PushButton) split[1]).ToolTip    = $"Remover {groupName}";
        }

        private static System.Windows.Media.Imaging.BitmapImage? LoadImage(string relativePath)
        {
            // assume que seus ícones estão em Resources\icons\... dentro do add-in folder
            string? folder = Path.GetDirectoryName(typeof(RibbonUI).Assembly.Location);
            if (folder == null) return null;

            string full   = Path.Combine(folder!, relativePath);
            if (!File.Exists(full)) return null;

            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(full);
            bmp.EndInit();
            return bmp;
        }
    }
}
