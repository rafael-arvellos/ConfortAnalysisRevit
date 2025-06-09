using Autodesk.Revit.DB;

namespace ConfortAnalysis.Data
{
    public static class ApplicationData
    {
        public static List<Reference> SelectedPlanarFacesRefs { get; private set; } = new List<Reference>();
        public static List<Reference> SelectedElementsRefs { get; private set; } = new List<Reference>();
        public static Dictionary<Reference, List<XYZ>> GeneratedPoints { get; private set; } = new Dictionary<Reference, List<XYZ>>();
            
    }

    public static class ApplicationDataFunctions
    {
        public static void ClearRefs(IList<Reference> refsList)
        {
            refsList.Clear();
        }
        public static void AddRefs(List<Reference> refsList, IList<Reference> refs)
        {
            refsList.AddRange(refs);
        }

        public static void ClearGeneratedPoints()
        {
            ApplicationData.GeneratedPoints.Clear();
        }

        public static void AddPointsForFace(Reference faceRef, IEnumerable<XYZ> points)
        {
            if (faceRef == null || points == null) return;
            if (!ApplicationData.GeneratedPoints.ContainsKey(faceRef))
                ApplicationData.GeneratedPoints[faceRef] = new List<XYZ>();

            ApplicationData.GeneratedPoints[faceRef].AddRange(points);
        }
        public static List<XYZ> GetPointsForFace(Reference faceRef)
        {
            if (faceRef == null) return new List<XYZ>();
            if (!ApplicationData.GeneratedPoints.TryGetValue(faceRef, out var list))
                return new List<XYZ>();

            return list;
        }
    }
}