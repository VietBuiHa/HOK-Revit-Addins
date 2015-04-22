﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Windows.Forms;
using Autodesk.Revit.DB.Architecture;


namespace HOK.DoorRoom
{
    public enum DoorLinkType
    {
        FindFromLink,
        CopyFromHost,
        None
    }
    public class DoorLinkManager
    {
        private UIApplication m_app;
        private Document m_doc;
        private DoorLinkType doorLinkType=DoorLinkType.None;
        
        private const string  toRoomNumber="ToRoomNumber";
        private const string toRoomName="ToRoomName";
        private const string fromRoomNumber="FromRoomNumber";
        private const string fromRoomName = "FromRoomName";

        private Dictionary<int, DoorProperties> doorDictionary = new Dictionary<int, DoorProperties>();
        private Dictionary<int, LinkedInstanceProperties> linkedInstanceDictionary = new Dictionary<int, LinkedInstanceProperties>();

        public DoorLinkManager(UIApplication uiapp, DoorLinkType linkType)
        {
            m_app = uiapp;
            m_doc = m_app.ActiveUIDocument.Document;
            doorLinkType = linkType;
            CollectDoors();
        }

        private void CollectDoors()
        {
            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(m_doc);
                List<FamilyInstance>  doorInstances = collector.OfClass(typeof(FamilyInstance)).OfCategory(BuiltInCategory.OST_Doors).WhereElementIsNotElementType().ToElements().Cast<FamilyInstance>().ToList();

                if (doorInstances.Count > 0)
                {
                    bool result = false;
                    switch (doorLinkType)
                    {
                        case DoorLinkType.CopyFromHost:
                            result = CopyRoomData(doorInstances);
                            if (result)
                            {
                                MessageBox.Show("System room data in all door elements are successfully copied in shared parameters.", "Completion Message - Door Link ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            break;
                        case DoorLinkType.FindFromLink:
                            Dictionary<int, LinkedRoomProperties> linkedRoomDictionary = CollectLinkedRooms();
                            result = CopyLinkedRoomData(doorInstances);
                            if (result)
                            {
                                MessageBox.Show("Room data from linked model are successfully propagated to shared parameters in door elements.", "Completion Message - Door Link", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            
                            break;
                    }
                }
                else
                {
                    MessageBox.Show("Door elements don't exist in the current Revit model.", "Doors Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to collect door elements.\n" + ex.Message, "Collect Doors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private bool CopyRoomData(List<FamilyInstance> doorInstances)
        {
            bool result = true;
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                using (Transaction trans = new Transaction(m_doc, "copy room data"))
                {
                    foreach (FamilyInstance door in doorInstances)
                    {
                        try
                        {
                            trans.Start();
#if RELEASE2015
                            DoorProperties dp = AssignToFromRoom(door);
                            if (null != dp.ToRoom)
                            {
                                string roomNumber = GetRoomNumber(dp.ToRoom);
                                string roomName = GetRoomName(dp.ToRoom);
                                Parameter toParam = door.LookupParameter(toRoomNumber);
                                if (null != toParam)
                                {
                                    toParam.Set(roomNumber);
                                }
                                toParam = door.LookupParameter(toRoomName);
                                if (null != toParam)
                                {
                                    toParam.Set(roomName);
                                }
                            }
                            if (null != dp.FromRoom)
                            {
                                string roomNumber = GetRoomNumber(dp.FromRoom);
                                string roomName = GetRoomName(dp.FromRoom);
                                Parameter fromParam = door.LookupParameter(fromRoomNumber);
                                if (null != fromParam)
                                {
                                    fromParam.Set(roomNumber);
                                }
                                fromParam = door.LookupParameter(fromRoomName);
                                if (null != fromParam)
                                {
                                    fromParam.Set(roomName);
                                }
                            }
#else
                            DoorProperties dp = AssignToFromRoom(door);
                            if (null != dp.ToRoom)
                            {
                                string roomNumber = GetRoomNumber(dp.ToRoom);
                                string roomName = GetRoomName(dp.ToRoom);
                                Parameter toParam = door.get_Parameter(toRoomNumber);
                                if (null != toParam)
                                {
                                    toParam.Set(roomNumber);
                                }
                                toParam = door.get_Parameter(toRoomName);
                                if (null != toParam)
                                {
                                    toParam.Set(roomName);
                                }
                            }
                            if (null != dp.FromRoom)
                            {
                                string roomNumber = GetRoomNumber(dp.FromRoom);
                                string roomName = GetRoomName(dp.FromRoom);
                                Parameter fromParam = door.get_Parameter(fromRoomNumber);
                                if (null != fromParam)
                                {
                                    fromParam.Set(roomNumber);
                                }
                                fromParam = door.get_Parameter(fromRoomName);
                                if (null != fromParam)
                                {
                                    fromParam.Set(roomName);
                                }
                            }
#endif
                            trans.Commit();
                        }
                        catch(Exception ex)
                        {
                            trans.RollBack();
                            result = false;
                            strBuilder.AppendLine(door.Id.IntegerValue+"\t"+door.Name+": "+ex.Message);
                        }
                    }
                    if (strBuilder.Length > 0)
                    {
                        MessageBox.Show("The following doors have been skipped due to some issues.\n\n" + strBuilder.ToString(), "Skipped Door Elements", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to copy system room data to shared parameters.\n"+ex.Message, "Copy Room Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                result = false;
            }
            return result;
        }

        private string GetRoomNumber(Room room)
        {
            string roomNumber = "";
            try
            {
                Parameter parameter = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                if (null != parameter)
                {
                    roomNumber = parameter.AsString();
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
            }
            return roomNumber;
        }

        private string GetRoomName(Room room)
        {
            string roomName = "";
            try
            {
                Parameter parameter = room.get_Parameter(BuiltInParameter.ROOM_NAME);
                if (null != parameter)
                {
                    roomName = parameter.AsString();
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
            }
            return roomName;
        }

        //sometimes Revit internal rooms cannot capture flipped doors correctly.
        private DoorProperties AssignToFromRoom(FamilyInstance door)
        {
            DoorProperties dp = new DoorProperties(door);
            try
            {
                if (null != dp.FromPoint && null != dp.ToPoint)
                {
                    Room roomA = door.ToRoom;
                    Room roomB = door.FromRoom;

                    if (null != roomA)
                    {
                        if (roomA.IsPointInRoom(dp.ToPoint))
                        {
                            dp.ToRoom = roomA;
                        }
                        else if (roomA.IsPointInRoom(dp.FromPoint))
                        {
                            dp.FromRoom = roomA;
                        }
                    }
                    if (null != roomB)
                    {
                        if (roomB.IsPointInRoom(dp.ToPoint))
                        {
                            dp.ToRoom = roomB;
                        }
                        else if (roomB.IsPointInRoom(dp.FromPoint))
                        {
                            dp.FromRoom = roomB;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to assign To and From values in door properties.\n"+ex.Message, "Assign To and From Room", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return dp;
        }
       
        private bool CopyLinkedRoomData(List<FamilyInstance> doorInstances)
        {
            bool result = true;
            try
            {
                StringBuilder strBuilder = new StringBuilder();
                using (Transaction trans = new Transaction(m_doc, "Set Parameter"))
                {
                    foreach (FamilyInstance door in doorInstances)
                    {
                        trans.Start();
                        try
                        {
                            DoorProperties dp = new DoorProperties(door);
                            if (null != dp.FromPoint && null != dp.ToPoint)
                            {
                                //bounding box filter
                                BoundingBoxXYZ bb = door.get_BoundingBox(null);
                                BoundingBoxXYZ bbExtended = new BoundingBoxXYZ();
                                bbExtended.Min = new XYZ(bb.Min.X - 5, bb.Min.Y - 5, bb.Min.Z);
                                bbExtended.Max = new XYZ(bb.Max.X + 5, bb.Max.Y + 5, bb.Max.Z);

                                Dictionary<int, LinkedRoomProperties> linkedRooms = new Dictionary<int, LinkedRoomProperties>();
                                foreach (LinkedInstanceProperties lip in linkedInstanceDictionary.Values)
                                {
                                    bbExtended.Transform = Transform.Identity;
                                    bbExtended.Transform = lip.TransformValue;
                                    Outline outline = new Outline(bbExtended.Min, bbExtended.Max);
                                    BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(outline);
                                    FilteredElementCollector collector = new FilteredElementCollector(lip.LinkedDocument);
                                    List<Room> roomList = collector.OfCategory(BuiltInCategory.OST_Rooms).WherePasses(bbFilter).WhereElementIsNotElementType().ToElements().Cast<Room>().ToList();
                                    if (roomList.Count > 0)
                                    {
                                        foreach (Room room in roomList)
                                        {
                                            LinkedRoomProperties lrp = new LinkedRoomProperties(room);
                                            lrp.LinkedInstance = lip;
                                            if (!linkedRooms.ContainsKey(lrp.RoomId))
                                            {
                                                linkedRooms.Add(lrp.RoomId, lrp);
                                            }
                                        }
                                    }
                                }
                                LinkedRoomProperties fromRoom = null;
                                LinkedRoomProperties toRoom = null;

                                if (linkedRooms.Count > 0)
                                {
                                    foreach (LinkedRoomProperties lrp in linkedRooms.Values)
                                    {
                                        XYZ tFrom = lrp.LinkedInstance.TransformValue.OfPoint(dp.FromPoint);
                                        XYZ tTo = lrp.LinkedInstance.TransformValue.OfPoint(dp.ToPoint);

                                        if (lrp.RoomObject.IsPointInRoom(tFrom))
                                        {
                                            dp.FromRoom = lrp.RoomObject;
                                            fromRoom = lrp;
                                        }
                                        if (lrp.RoomObject.IsPointInRoom(tTo))
                                        {
                                            dp.ToRoom = lrp.RoomObject;
                                            toRoom = lrp;
                                        }
                                    }
                                }

                                if (null != fromRoom)
                                {
#if RELEASE2015
                                    Parameter fParam = door.LookupParameter(fromRoomNumber);
                                    if (null != fParam)
                                    {
                                        fParam.Set(fromRoom.RoomNumber);
                                    }
                                    fParam = door.LookupParameter(fromRoomName);
                                    if (null != fParam)
                                    {
                                        fParam.Set(fromRoom.RoomName);
                                    }
#else
                                    Parameter fParam = door.get_Parameter(fromRoomNumber);
                                    if (null != fParam)
                                    {
                                        fParam.Set(fromRoom.RoomNumber);
                                    }
                                    fParam = door.get_Parameter(fromRoomName);
                                    if (null != fParam)
                                    {
                                        fParam.Set(fromRoom.RoomName);
                                    }
#endif
                                }


                                if (null != toRoom)
                                {
#if RELEASE2015
                                    Parameter tParam = door.LookupParameter(toRoomNumber);
                                    if (null != tParam)
                                    {
                                        tParam.Set(toRoom.RoomNumber);
                                    }
                                    tParam = door.LookupParameter(toRoomName);
                                    if (null != tParam)
                                    {
                                        tParam.Set(toRoom.RoomName);
                                    }
#else
                                     Parameter tParam = door.get_Parameter(toRoomNumber);
                                    if (null != tParam)
                                    {
                                        tParam.Set(toRoom.RoomNumber);
                                    }
                                    tParam = door.get_Parameter(toRoomName);
                                    if (null != tParam)
                                    {
                                        tParam.Set(toRoom.RoomName);
                                    }
#endif
                                }

                                if (!doorDictionary.ContainsKey(dp.DoorId))
                                {
                                    doorDictionary.Add(dp.DoorId, dp);
                                }
                            }
                            trans.Commit();
                        }
                        catch (Exception ex)
                        {
                            trans.RollBack();
                            result = false;
                            strBuilder.AppendLine(door.Id.IntegerValue + "\t" + door.Name + ": " + ex.Message);
                        }
                    }
                    if (strBuilder.Length > 0)
                    {
                        MessageBox.Show("The following doors have been skipped due to some issues.\n\n" + strBuilder.ToString(), "Skipped Door Elements", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to collect door data.\n"+ex.Message, "Collect Door Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return result;
        }

        private Dictionary<int, LinkedRoomProperties> CollectLinkedRooms()
        {
            Dictionary<int, LinkedRoomProperties>  linkedRoomDictionary = new Dictionary<int, LinkedRoomProperties>();
            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(m_doc);
                collector.OfCategory(BuiltInCategory.OST_RvtLinks).WhereElementIsNotElementType();
                List<RevitLinkInstance> revitLinkInstances = collector.ToElements().Cast<RevitLinkInstance>().ToList();

                Dictionary<int/*typeId*/, RevitLinkType> linkTypes = new Dictionary<int, RevitLinkType>();
                foreach (RevitLinkInstance instance in revitLinkInstances)
                {
#if RELEASE2013
                    if (null == instance.Document) { continue; }
            
#elif RELEASE2014 || RELEASE2015
                    if (null == instance.GetLinkDocument()) { continue; }
#endif
                    LinkedInstanceProperties lip = new LinkedInstanceProperties(instance);

                    FilteredElementCollector linkedCollector = new FilteredElementCollector(lip.LinkedDocument);
                    List<Room> rooms = linkedCollector.OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().ToElements().Cast<Room>().ToList();
                    if (rooms.Count > 0)
                    {
                        if (!linkedInstanceDictionary.ContainsKey(lip.InstanceId))
                        {
                            linkedInstanceDictionary.Add(lip.InstanceId, lip);
                        }

                        foreach (Room room in rooms)
                        {
                            LinkedRoomProperties lrp = new LinkedRoomProperties(room);
                            lrp.LinkedInstance = lip;
                            if (!linkedRoomDictionary.ContainsKey(lrp.RoomId))
                            {
                                linkedRoomDictionary.Add(lrp.RoomId, lrp);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to collect linked rooms.\n"+ex.Message, "Collect Linked Rooms", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return linkedRoomDictionary;
        }
        
    }

    public class DoorProperties
    {
        private FamilyInstance doorInstance = null;
        private int doorId = 0;
        private Room toRoom = null;
        private Room fromRoom = null;
        private XYZ toPoint = null;
        private XYZ fromPoint = null;

        public FamilyInstance DoorInstance { get { return doorInstance; } set { doorInstance = value; } }
        public int DoorId { get { return doorId; } set { doorId = value; } }
        public Room ToRoom { get { return toRoom; } set { toRoom = value; } }
        public Room FromRoom { get { return fromRoom; } set { fromRoom = value; } }
        public XYZ ToPoint { get { return toPoint; } set { toPoint = value; } }
        public XYZ FromPoint { get { return fromPoint; } set { fromPoint = value; } }

        public DoorProperties(FamilyInstance instance)
        {
            doorInstance = instance;
            doorId = instance.Id.IntegerValue;
            CreateDoorPoints();
        }

        public void CreateDoorPoints()
        {
            try
            {
                BoundingBoxXYZ bb = doorInstance.get_BoundingBox(null);
                if (null != bb)
                {
                    XYZ insertionPoint = new XYZ(0.5 * (bb.Min.X + bb.Max.X), 0.5 * (bb.Min.Y + bb.Max.Y), 0.5 * (bb.Min.Z + bb.Max.Z));
                    XYZ direction = doorInstance.FacingOrientation;
                    double offset = 3;

                    toPoint = insertionPoint + offset * direction.Normalize();
                    fromPoint = insertionPoint + offset * direction.Negate().Normalize();
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
            }
        }
    }

    public class LinkedRoomProperties
    {
        private Room roomObject = null;
        private int roomId = 0;
        private string roomName = "";
        private string roomNumber = "";
        private LinkedInstanceProperties lip = null;

        public Room RoomObject { get { return roomObject; } set { roomObject = value; } }
        public int RoomId { get { return roomId; } set { roomId = value; } }
        public string RoomName { get { return roomName; } set { roomName = value; } }
        public string RoomNumber { get { return roomNumber; } set { roomNumber = value; } }
        public LinkedInstanceProperties LinkedInstance { get { return lip; } set { lip = value; } }

        public LinkedRoomProperties(Room room)
        {
            roomObject = room;
            roomId = room.Id.IntegerValue;
            Parameter param = room.get_Parameter(BuiltInParameter.ROOM_NAME);
            if (null != param)
            {
                roomName = param.AsString();
            }
            param = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
            if (null != param)
            {
                roomNumber = param.AsString();
            }
        }
    }

    public class LinkedInstanceProperties
    {
        private RevitLinkInstance m_instance = null;
        private int instanceId = -1;
        private Document linkedDocument = null;
        private string documentTitle = "";
        private Autodesk.Revit.DB.Transform transformValue = null;

        public RevitLinkInstance Instance { get { return m_instance; } set { m_instance = value; } }
        public int InstanceId { get { return instanceId; } set { instanceId = value; } }
        public Document LinkedDocument { get { return linkedDocument; } set { linkedDocument = value; } }
        public string DocumentTitle { get { return documentTitle; } set { documentTitle = value; } }
        public Autodesk.Revit.DB.Transform TransformValue { get { return transformValue; } set { transformValue = value; } }

        public LinkedInstanceProperties(RevitLinkInstance instance)
        {
            m_instance = instance;
            instanceId = instance.Id.IntegerValue;
#if RELEASE2013
            linkedDocument = instance.Document;
#elif RELEASE2014 || RELEASE2015
            linkedDocument = instance.GetLinkDocument();
#endif
            documentTitle = linkedDocument.Title;
            transformValue = instance.GetTotalTransform();
        }
    }
}