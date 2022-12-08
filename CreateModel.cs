using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.ApplicationServices;


namespace RevitAPIUI10CreateModel
{
    [Transaction(TransactionMode.Manual)]
    public class CreateModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;

            GetWalls(commandData);

            List<Level> levels = GetLevels(commandData);
            List<Wall> walls = GetWallTypes(commandData);

            Transaction ts1 = new Transaction(doc, "Построение дверей");
            {
                ts1.Start();
                AddDoor(commandData, levels.Where(x => x.Name.Equals("Уровень 1")).FirstOrDefault(), walls[0]);
                ts1.Commit();
            }

            Transaction ts2 = new Transaction(doc, "Построение окон");
            {
                ts2.Start();
                foreach (Wall wall in walls)
                {
                    if (!wall.Equals(walls[0]))
                    AddWindow(commandData, levels.Where(x => x.Name.Equals("Уровень 1")).FirstOrDefault(), wall);
                    
                }
                ts2.Commit();
            }

            Transaction ts3 = new Transaction(doc, "Добавление уровня крыши");
            {
                ts3.Start();
                AddRoof(commandData, levels.Where(x => x.Name.Equals("Уровень 2")).FirstOrDefault(), walls);

                double elevValue = 4125;
                double elevation = UnitUtils.ConvertToInternalUnits(elevValue, UnitTypeId.Millimeters);
                Level levelRoof = Level.Create(doc, elevation);
                levels.Add(levelRoof);

                AddRoofExtrusion(commandData, levelRoof, walls, 32.8, 16.4);
                ts3.Commit();
            }

            return Result.Succeeded;
        }

        public static List<Level> GetLevels(ExternalCommandData commandData)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> levels = new FilteredElementCollector(doc)
                                                       .OfClass(typeof(Level))
                                                       .Cast<Level>()
                                                       .ToList();
           

            return levels;
        }

        public static List<XYZ> GetPoints()
        {
            double width = UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(5000, UnitTypeId.Millimeters);

            double dx = width / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            return points;
        }

        public static List<Wall> GetWalls(ExternalCommandData commandData)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            List<Level> levels = GetLevels(commandData);
            List<XYZ> points = GetPoints();
            List<Wall> walls = new List<Wall>();

            Level level1 = levels
              .Where(x => x.Name.Equals("Уровень 1"))
              .FirstOrDefault();
            Level level2 = levels
                .Where(x => x.Name.Equals("Уровень 2"))
                .FirstOrDefault();

            Transaction ts = new Transaction(doc, "Построение стен");
            {
                ts.Start();
                for (int i = 0; i < 4; i++)
                {
                    Line line = Line.CreateBound(points[i], points[i + 1]);
                    Wall wall = Wall.Create(doc, line, level1.Id, false);
                    walls.Add(wall);
                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);
                }
                ts.Commit();
            }

            return walls;
        }

        private static void AddDoor(ExternalCommandData commandData, Level level, Wall wall)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            List<Level> levels = GetLevels(commandData);

            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ midPoint = (point1 + point2) / 2;
            
            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(midPoint, doorType, wall, levels.Where(x => x.Name.Equals("Уровень 1")).FirstOrDefault(), StructuralType.NonStructural);
        }

        private static void AddWindow(ExternalCommandData commandData, Level level, Wall wall)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> levels = GetLevels(commandData);
            List<Wall> walls = GetWallTypes(commandData);

            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0610 x 1220 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ midPoint1 = (point1 + point2) / 2;
            XYZ midPoint2 = GetElementCenter(wall);
            XYZ offset = (midPoint1 + midPoint2) / 2;

            if (!windowType.IsActive)
                windowType.Activate();

            doc.Create.NewFamilyInstance(offset, windowType, wall, levels.Where(x => x.Name.Equals("Уровень 1")).FirstOrDefault(), StructuralType.NonStructural);
        }

        public static XYZ GetElementCenter(Element element)
        {
            BoundingBoxXYZ bounding = element.get_BoundingBox(null);
            return (bounding.Min + bounding.Max) / 2;
        }

        public static List<Wall> GetWallTypes(ExternalCommandData commandData)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            var walllist = new FilteredElementCollector(doc)
                                                       .OfClass(typeof(Wall))
                                                       .Cast<Wall>()
                                                       .ToList();

            return walllist;
        }

        private static void AddRoof(ExternalCommandData commandData, Level level2, List<Wall> walls)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            Application application = doc.Application;

            List<Level> levels = GetLevels(commandData);
            
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 125мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dt, -dt, 0));
            points.Add(new XYZ(dt, -dt, 0));
            points.Add(new XYZ(dt, dt, 0));
            points.Add(new XYZ(-dt, dt, 0));
            points.Add(new XYZ(-dt, -dt, 0));

            CurveArray footprint = application.Create.NewCurveArray();

            for (int i = 0; i < 4; i++)
            {
                LocationCurve curve = walls[i].Location as LocationCurve;
                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + points[i], p2 + points[i + 1]);
                footprint.Append(line);
            }

            ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
            FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footprint, level2, roofType, out footPrintToModelCurveMapping);

            //ModelCurveArrayIterator iterator = footPrintToModelCurveMapping.ForwardIterator();
            //iterator.Reset();
            //while (iterator.MoveNext())
            //{
            //    ModelCurve modelCurve = iterator.Current as ModelCurve;
            //    footprintRoof.set_DefinesSlope(modelCurve, true);
            //    footprintRoof.set_SlopeAngle(modelCurve, 0.5);
            //}

            //foreach (ModelCurve m in footPrintToModelCurveMapping)
            //{
            //    footprintRoof.set_DefinesSlope(m, true);
            //    footprintRoof.set_SlopeAngle(m, 0.5);
            //}
        }

        private static void AddRoofExtrusion(ExternalCommandData commandData, Level level2, List<Wall> walls, double width, double depth)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            View v = doc.ActiveView;

            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 125мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            double extrusionStart = -width / 2 - dt;
            double extrusionEnd = width / 2 + dt;

            double curveStart = -depth / 2 - dt;
            double curveEnd = +depth / 2 + dt;

            //double elevValue = 4125;
            //double elevation = UnitUtils.ConvertToInternalUnits(elevValue, UnitTypeId.Millimeters);
            //Level levelRoof = Level.Create(doc, elevation);

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(0, curveStart, level2.Elevation), new XYZ(0, 0, level2.Elevation + 10)));
            curveArray.Append(Line.CreateBound(new XYZ(0, 0, level2.Elevation + 10), new XYZ(0, curveEnd, level2.Elevation)));

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), v);
            ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curveArray, plane, level2, roofType, extrusionStart, extrusionEnd);
            extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;
        }
    }
}
