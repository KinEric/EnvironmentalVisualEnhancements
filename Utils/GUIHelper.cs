﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Utils
{

    public class GUIHelper
    {
        public const float spacingOffset = .25f;
        public const float elementHeight = 22;
        public const float valueRatio = (3f / 7);

        protected static int selectedBodyIndex = 0;
        protected static CelestialBody currentBody;
        
        public static float GetNodeHeightCount(ConfigNode node, Type T)
        {
            float fieldCount = 1f+ (2f*spacingOffset);
            var objfields = T.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(
                    f => Attribute.IsDefined(f, typeof(Persistent)));
            

            foreach (FieldInfo field in objfields)
            {
                bool isNode = ConfigHelper.IsNode(field, node, false);

                if ( node != null && ConfigHelper.ConditionsMet(field, node))
                {
                    if (isNode)
                    {
                        if (node.HasNode(field.Name))
                        {
                            fieldCount += GetNodeHeightCount(node.GetNode(field.Name), field.FieldType);
                        }
                        else
                        {
                            fieldCount += GetNodeHeightCount(null, field.FieldType);
                        }
                        fieldCount += spacingOffset;
                    }
                    else if(field.Name != "value")
                    {
                        fieldCount += 1f+ spacingOffset;
                    }
                }
            }

            return fieldCount;
        }
        
        public static Rect GetRect(Rect placementBase, ref Rect placement, ConfigNode node, Type T)
        {
            placement.height = GetNodeHeightCount(node, T);
            float width = placementBase.width;
            float height;

            float x = (placement.x * width) + placementBase.x;
            float y = (placement.y * elementHeight) + placementBase.y;
            width += placement.width;
            height = (elementHeight * placement.height);
            return new Rect(x, y, width - 10, height);
        }

        public static Rect GetRect(Rect placementBase, ref Rect placement)
        {
            float width = placementBase.width;
            float height;

            float x = (placement.x * width) + placementBase.x;
            float y = (placement.y * elementHeight) + placementBase.y;
            width += placement.width;
            height = (elementHeight * placement.height);
            return new Rect(x, y, width - 10, height);
        }

        public static void SplitRect(ref Rect rect1, ref Rect rect2, float ratio)
        {
            float width = rect1.width;
            rect1.width *= ratio;
            if (rect2 != null)
            {
                rect2.width = width * (1 - ratio);
                rect2.x = rect1.x + rect1.width;
            }
        }

        private static CelestialBody GetMapBody()
        {
            if (MapView.MapIsEnabled)
            {
                MapObject target = MapView.MapCamera.target;
                switch (target.type)
                {
                    case MapObject.MapObjectType.CELESTIALBODY:
                        return target.celestialBody;
                    case MapObject.MapObjectType.MANEUVERNODE:
                        return target.maneuverNode.patch.referenceBody;
                    case MapObject.MapObjectType.VESSEL:
                        return target.vessel.mainBody;
                }
            }
            else if (HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                CelestialBody[] celestialBodies = FlightGlobals.Bodies.ToArray();
                return celestialBodies.First<CelestialBody>(cb => cb.isHomeWorld);
            }
            else
            {
                return currentBody = FlightGlobals.currentMainBody;
            }
            return null;
        }

        public static string DrawBodySelector(Rect placementBase, ref Rect placement)
        {
            CelestialBody[] celestialBodies = FlightGlobals.Bodies.ToArray();
            GUIStyle gsCenter = new GUIStyle(GUI.skin.label);
            gsCenter.alignment = TextAnchor.MiddleCenter;

            currentBody = GUIHelper.GetMapBody();

            Rect leftRect = GetRect(placementBase, ref placement);
            Rect centerRect = GetRect(placementBase, ref placement);
            Rect rightRect = GetRect(placementBase, ref placement);
            SplitRect(ref leftRect, ref centerRect, 1f / 4);
            SplitRect(ref centerRect, ref rightRect, 2f / 3);
            if (MapView.MapIsEnabled || HighLogic.LoadedScene == GameScenes.TRACKSTATION)
            {
                if (GUI.Button(leftRect, "<"))
                {
                    selectedBodyIndex--;
                    if (selectedBodyIndex < 0)
                    {
                        selectedBodyIndex = celestialBodies.Length - 1;
                    }
                    currentBody = celestialBodies[selectedBodyIndex];
                    MapView.MapCamera.SetTarget(currentBody.bodyName);
                }
                if (GUI.Button(rightRect, ">"))
                {
                    selectedBodyIndex++;
                    if (selectedBodyIndex >= celestialBodies.Length)
                    {
                        selectedBodyIndex = 0;
                    }
                    currentBody = celestialBodies[selectedBodyIndex];
                    MapView.MapCamera.SetTarget(currentBody.bodyName);
                }
            }
            else
            {
                if (FlightGlobals.currentMainBody != null)
                {
                    currentBody = FlightGlobals.currentMainBody;
                    selectedBodyIndex = Array.FindIndex<CelestialBody>(celestialBodies, cb => cb.name == currentBody.name);
                }
            }
            if (currentBody != null)
            {
                GUI.Label(centerRect, currentBody.bodyName, gsCenter);
            }
            placement.y++;
            return currentBody.bodyName;
        }

        public static void DrawField(Rect placementBase, ref Rect placement, object obj, FieldInfo field, ConfigNode config)
        {
            placement.height = 1;
            String value = config.GetValue(field.Name);
            String defaultValue = ConfigHelper.CreateConfigFromObject(obj, new ConfigNode("TMP")).GetValue(field.Name);
            if (value == null)
            {
                if (defaultValue == null)
                {
                    defaultValue = "";
                }
                value = defaultValue;
            }

            Rect labelRect = GUIHelper.GetRect(placementBase, ref placement);
            Rect fieldRect = GUIHelper.GetRect(placementBase, ref placement);
            GUIHelper.SplitRect(ref labelRect, ref fieldRect, valueRatio);
            String tooltipText = "";
            if (Attribute.IsDefined(field, typeof(TooltipAttribute)))
            {
                TooltipAttribute tt = (TooltipAttribute)Attribute.GetCustomAttribute(field, typeof(TooltipAttribute));
                tooltipText = tt.tooltip;
            }
            GUIStyle style = new GUIStyle(GUI.skin.label);
            GUIContent gc = new GUIContent(field.Name, tooltipText);

            Vector2 labelSize = style.CalcSize(gc);
            labelRect.width = Mathf.Min(labelSize.x, labelRect.width);
            GUI.Label(labelRect,gc);

            string newValue = value;
            if (field.FieldType.IsEnum)
            {

                newValue = ComboBox(fieldRect, value, Enum.GetNames(field.FieldType));
            }
            else
            {
                GUIStyle fieldStyle = new GUIStyle(GUI.skin.textField);
                if (value != "" && !ConfigHelper.CanParse(field, value))
                {
                    fieldStyle.normal.textColor = Color.red;
                    fieldStyle.active.textColor = Color.red;
                    fieldStyle.focused.textColor = Color.red;
                    fieldStyle.hover.textColor = Color.red;
                }
                newValue = GUI.TextField(fieldRect, value, fieldStyle);
            }

            if (newValue != defaultValue && value != newValue)
            {
                if (config.HasValue(field.Name))
                {
                    config.SetValue(field.Name, newValue);
                }
                else
                {
                    config.AddValue(field.Name, newValue);
                }
            }
            else if(newValue == defaultValue && config.HasValue(field.Name))
            {
                config.RemoveValue(field.Name);
            }
            placement.y += 1f;
        }

        private static string ComboBox(Rect fieldRect, string value, string[] list)
        {
            GUIStyle gs = new GUIStyle(GUI.skin.textField);
            GUI.Box(fieldRect, value, gs);
            fieldRect.x += fieldRect.width - (2* elementHeight);
            fieldRect.width = elementHeight;
            Rect fieldRectUp = new Rect(fieldRect);
            Rect fieldRectDown = new Rect(fieldRect);
            fieldRectDown.x += fieldRectDown.width;
            //gs.font.RequestCharactersInTexture("\u25b6\u25c0");
            if (GUI.Button(fieldRectUp, "\u25c0", gs))
            {
                int index = Array.IndexOf(list, value);
                index--;
                if(index < 0)
                {
                    index = list.Length-1;
                }
                value = list[index];
            }
            if(GUI.Button(fieldRectDown, "\u25b6", gs))
            {
                int index = Array.IndexOf(list, value);
                index++;
                if (index >= list.Length)
                {
                    index = 0;
                }
                value = list[index];
            }
            return value;
        }

        public static ConfigNode DrawObjectSelector(ConfigNode.ConfigNodeList nodeList, ref int selectedObjIndex, ref String objString, ref Vector2 objListPos, Rect placementBase, ref Rect placement)
        {
            String[] objList = nodeList.DistinctNames();
            float nodeHeight = placement.height;
            Rect selectBoxOutlineRect = GetRect(placementBase, ref placement);
            placement.height = nodeHeight-1;
            Rect selectBoxRect = GetRect(placementBase, ref placement);
            placement.height = nodeList.Count;
            Rect selectBoxItemsRect = GetRect(placementBase, ref placement);
            
            placement.height = 1;
            Rect optButtonRect = GetRect(placementBase, ref placement);
            SplitRect(ref selectBoxRect, ref optButtonRect, (15f / 16));


            GUI.Box(selectBoxOutlineRect, "");
            selectBoxRect.x += 10;
            selectBoxRect.width -= 20;
            selectBoxRect.y += 10;
            selectBoxRect.height -= 20;
            
            selectBoxItemsRect.x = 0;
            selectBoxItemsRect.y = 0;
            if (nodeList.Count <= 3)
            {
                selectBoxItemsRect.width = selectBoxRect.width;
            }
            else
            {
                selectBoxItemsRect.width = selectBoxRect.width - 20;
            }

            objListPos = GUI.BeginScrollView(selectBoxRect, objListPos, selectBoxItemsRect);
            int oldselectedObjIndex = selectedObjIndex;
            if (selectedObjIndex == -1)
            {
                selectedObjIndex = 0;
            }
            selectedObjIndex = GUI.SelectionGrid(selectBoxItemsRect, selectedObjIndex, objList, 1);
            GUI.EndScrollView();
            placement.y += nodeHeight-1;

            optButtonRect.x -= 5;
            optButtonRect.y += 10;
            if (nodeList.Count > 0)
            {
                if (GUI.Button(optButtonRect, "^") && selectedObjIndex > 0)
                {
                    int moveIndex = selectedObjIndex;
                    ConfigNode item = nodeList[--selectedObjIndex];
                    nodeList.Remove(item);
                    nodeList.Add(item);
                    for(int i = moveIndex; i < nodeList.Count-1; i++)
                    {
                        item = nodeList[moveIndex];
                        nodeList.Remove(item);
                        nodeList.Add(item);
                    }
                }
                optButtonRect.y += optButtonRect.height*(nodeHeight-3);
                if (GUI.Button(optButtonRect, "v") && selectedObjIndex < nodeList.Count - 1)
                {
                    
                    ConfigNode item = nodeList[selectedObjIndex];
                    nodeList.Remove(item);
                    nodeList.Add(item);
                    int moveIndex = ++selectedObjIndex;
                    for(int i = moveIndex; i < nodeList.Count-1; i++)
                    {
                        item = nodeList[moveIndex];
                        nodeList.Remove(item);
                        nodeList.Add(item);
                    }
                }
            }
            Rect listEditTextRect = GetRect(placementBase, ref placement);
            listEditTextRect.x += 10;
            listEditTextRect.width -= 20;
            listEditTextRect.y -= 5;

            Rect listEditRect = new Rect(listEditTextRect);
            Rect listAddRect = new Rect(listEditTextRect);
            Rect listRemoveRect = new Rect(listEditTextRect);
            
            SplitRect(ref listEditTextRect, ref listEditRect, (1f / 2));
            SplitRect(ref listEditRect, ref listAddRect, (1f / 3));
            SplitRect(ref listAddRect, ref listRemoveRect, (1f / 2));

            listEditTextRect.width -= 5;
            listEditRect.width -= 5;
            listAddRect.width -= 5;
            listRemoveRect.width -= 5;

            if (selectedObjIndex != oldselectedObjIndex && nodeList.Count > 0)
            {
                objString = nodeList[selectedObjIndex].name;
            }
            objString = GUI.TextField(listEditTextRect, objString);
            String name = objString;
            if (objString.Length > 0 && !objString.Contains(' ') && !nodeList.Contains(objString))
            {
                if(nodeList.Count > 0 && GUI.Button(listEditRect, "#"))
                {
                    nodeList[selectedObjIndex].name = objString;
                }
                if(GUI.Button(listAddRect, "+"))
                {
                    nodeList.Add(new ConfigNode(objString));
                }
            }
            else
            {
                listEditRect.width += listAddRect.width;
                GUI.Label(listEditRect, "Invalid Name!");
            }
            if (nodeList.Count > 0 && GUI.Button(listRemoveRect, "-"))
            {
                ConfigNode item = nodeList[selectedObjIndex];
                nodeList.Remove(item);
                if(selectedObjIndex >= nodeList.Count)
                {
                    selectedObjIndex = nodeList.Count -1;
                }
            }
            placement.y +=1 + spacingOffset;
            
            if(nodeList.Count == 0)
            {
                return null;
            }
            return nodeList[selectedObjIndex];
        }

        public static T DrawSelector<T>(List<T> objList, ref int selectedIndex, float ratio, Rect placementBase, ref Rect placement) where T : INamed
        {
            Rect leftRect = GUIHelper.GetRect(placementBase, ref placement);
            Rect centerRect = GUIHelper.GetRect(placementBase, ref placement);
            Rect rightRect = GUIHelper.GetRect(placementBase, ref placement);
            GUIHelper.SplitRect(ref leftRect, ref centerRect, 1f / (ratio));
            GUIHelper.SplitRect(ref centerRect, ref rightRect, (ratio - 2) / (ratio-1));

            if (objList.Count > 1 && GUI.Button(leftRect, "<"))
            {
                selectedIndex--;
                if (selectedIndex < 0)
                {
                    selectedIndex = objList.Count - 1;
                }
            }
            if (objList.Count > 1 && GUI.Button(rightRect, ">"))
            {
                selectedIndex++;
                if (selectedIndex >= objList.Count)
                {
                    selectedIndex = 0;
                }
            }
            T currentObj = default(T);
            if (selectedIndex < objList.Count)
            {
                currentObj = objList[selectedIndex];
                if (currentObj != null)
                {
                    GUIStyle gsCenter = new GUIStyle(GUI.skin.label);
                    gsCenter.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(centerRect, currentObj.Name, gsCenter);
                }

            }
            placement.y += 1 + spacingOffset;
            return currentObj;
        }

        public static void HandleGUI(object obj, ConfigNode configNode, Rect placementBase, ref Rect placement)
        {

            var objfields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(
                    field => Attribute.IsDefined(field, typeof(Persistent)));
            foreach (FieldInfo field in objfields)
            {
                bool isNode = ConfigHelper.IsNode(field, configNode);
                bool isNodeOptional = Attribute.IsDefined(field, typeof(NodeOptional));

                
                if (isNode || isNodeOptional)
                {
                    placement.y += spacingOffset;
                    bool isOptional = Attribute.IsDefined(field, typeof(Optional));

                    ConfigNode node = configNode.GetNode(field.Name);
                    GUIStyle gsRight = new GUIStyle(GUI.skin.label);
                    gsRight.alignment = TextAnchor.MiddleCenter;

                    
                    Rect boxRect = GUIHelper.GetRect(placementBase, ref placement, node, field.FieldType);
                    GUIStyle gs = new GUIStyle(GUI.skin.textField);
                    GUI.Box(boxRect, "", gs);
                    placement.height = 1;
                    placement.y += spacingOffset;

                    Rect boxPlacementBase = new Rect(placementBase);
                    boxPlacementBase.x += 10;
                    Rect boxPlacement = new Rect(placement);
                    boxPlacement.width -= 20;

                    Rect toggleRect = GUIHelper.GetRect(boxPlacementBase, ref boxPlacement);
                    Rect titleRect = GUIHelper.GetRect(boxPlacementBase, ref boxPlacement);
                    Rect fieldRect = GUIHelper.GetRect(placementBase, ref boxPlacement);
                    if (isNodeOptional)
                    {
                        GUIHelper.SplitRect(ref titleRect, ref fieldRect, valueRatio);
                    }
                    GUIHelper.SplitRect(ref toggleRect, ref titleRect, (1f / 16));

                    String tooltipText = "";
                    if (Attribute.IsDefined(field, typeof(TooltipAttribute)))
                    {
                        TooltipAttribute tt = (TooltipAttribute)Attribute.GetCustomAttribute(field, typeof(TooltipAttribute));
                        tooltipText = tt.tooltip;
                    }
                    GUIStyle style = new GUIStyle(GUI.skin.label);
                    GUIContent gc = new GUIContent(field.Name, tooltipText);

                    Vector2 labelSize = style.CalcSize(gc);
                    titleRect.width = Mathf.Min(labelSize.x, titleRect.width);
                    GUI.Label(titleRect, gc);

                    bool removeable = node == null ? false : true;
                    if (isOptional || isNodeOptional)
                    {
                        String value = null;
                        String defaultValue = ConfigHelper.CreateConfigFromObject(obj, new ConfigNode("TMP")).GetValue(field.Name);
                        String newValue = "";
                        if (isNodeOptional)
                        {
                            if (configNode.HasValue(field.Name))
                            {
                                value = configNode.GetValue(field.Name);
                            }
                            else if (node != null && node.HasValue("value"))
                            {
                                value = node.GetValue("value");
                            }
                            
                            if (value == null)
                            {
                                if (defaultValue == null)
                                {
                                    defaultValue = "";
                                }
                                value = defaultValue;
                            }

                            GUIStyle fieldStyle = new GUIStyle(GUI.skin.textField);
                            if (value != "" && !ConfigHelper.CanParse(field, value))
                            {
                                fieldStyle.normal.textColor = Color.red;
                                fieldStyle.active.textColor = Color.red;
                                fieldStyle.focused.textColor = Color.red;
                                fieldStyle.hover.textColor = Color.red;
                            }
                            newValue = GUI.TextField(fieldRect, value, fieldStyle);

                        }
                        bool toggle = removeable != GUI.Toggle(toggleRect, removeable, "");
                        if (toggle)
                        {
                            if (removeable)
                            {
                                configNode.RemoveNode(field.Name);
                                node = null;
                            }
                            else
                            {
                                node = configNode.AddNode(new ConfigNode(field.Name));
                            }
                        }

                        if (isNodeOptional)
                        {
                            if ((newValue != defaultValue && value != newValue) || toggle)
                            {
                                if (newValue != defaultValue)
                                {
                                    if (node != null)
                                    {

                                        if (node.HasValue("value"))
                                        {
                                            node.SetValue("value", newValue);
                                        }
                                        else
                                        {
                                            node.AddValue("value", newValue);
                                        }
                                    }
                                    else
                                    {
                                        if (configNode.HasValue(field.Name))
                                        {
                                            configNode.SetValue(field.Name, newValue);
                                        }
                                        else
                                        {
                                            configNode.AddValue(field.Name, newValue);
                                        }
                                    }
                                }
                                else if (newValue == defaultValue)
                                {
                                    if (node != null)
                                    {
                                        if (node.HasValue("value"))
                                        {
                                            node.RemoveValue("value");
                                        }
                                    }
                                    else
                                    {
                                        if (configNode.HasValue(field.Name))
                                        {
                                            configNode.RemoveValue(field.Name);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (node == null)
                    {
                        node = configNode.AddNode(new ConfigNode(field.Name));
                    }
                    boxPlacement.y += 1f;
                    if (node != null)
                    {
                        object subObj = field.GetValue(obj);
                        if (subObj == null)
                        {
                            ConstructorInfo ctor = field.FieldType.GetConstructor(System.Type.EmptyTypes);
                            subObj = ctor.Invoke(null);
                        }

                        HandleGUI(subObj, node, boxPlacementBase, ref boxPlacement);

                    }
                    boxPlacement.y += spacingOffset;

                    placement.y = boxPlacement.y;
                    placement.x = boxPlacement.x;

                }
                else
                {
                    if (ConfigHelper.ConditionsMet(field, configNode))
                    {
                        if (field.Name != "body" && field.Name != "value")
                        {
                            placement.y += spacingOffset;
                            GUIHelper.DrawField(placementBase, ref placement, obj, field, configNode);
                        }
                    }
                    else if(configNode.HasValue(field.Name))
                    {
                        configNode.RemoveValue(field.Name);
                    }
                }
            }
            
        }

    }
}
