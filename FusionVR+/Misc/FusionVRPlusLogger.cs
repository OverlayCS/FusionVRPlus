using UnityEditor;
using UnityEngine;

namespace FusionVRPlus.Misc
{
    public enum MessageType
    {
        Info,
        Warning,
        Error,
        Fatal
    }

    public class FusionVRPlusLogger
    {
        public static void PrintMessage(string message, MessageType type)
        {
            switch(type)
            {
                case MessageType.Info:
                    Debug.Log($"<color=#002aff>[FusionVRPlus] </color> {message}");
                    break;
                case MessageType.Warning:
                    Debug.LogWarning($"<color=#ff9100>[FusionVRPlus] </color> {message}");
                    break;
                case MessageType.Error:
                    Debug.LogError($"<color=#ff2a00>[FusionVRPlus] </color> {message}");
                    break;
                case MessageType.Fatal:
                    Debug.LogError($"<color=#ff2a00>[FusionVRPlus] </color> {message}");
                    break;
            }
        }
    }
}
