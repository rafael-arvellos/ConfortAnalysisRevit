using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Reflection;

namespace ConfortAnalysis
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            Views.RibbonUI.CreateRibbonUI(application);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            return Result.Succeeded;
        }
    }
}