using Horde;
using UnityEditor;
using UnityEngine.UIElements;

namespace Dev_Tools
{
    [CustomEditor(typeof(HordeController))]
    public class HordeInspector : Editor
    {
        public VisualTreeAsset m_InspectorXML;

        public override VisualElement CreateInspectorGUI()
        {
            // Create a new VisualElement to be the root of our Inspector UI.
            VisualElement myInspector = new VisualElement();
            m_InspectorXML = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Dev Tools/horde-inspector.uxml");
            myInspector = m_InspectorXML.Instantiate();
            return myInspector;
        }
    }
}