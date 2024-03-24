using Aki.Reflection.Utils;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using QuestClass = GClass1249;

namespace DrakiaXYZ.QuestTracker.Helpers
{
    internal class Utils
    {
        private static MethodInfo _stringLocalizedMethod;

        private static PropertyInfo _questControllerQuestsProperty;
        private static MethodInfo _getQuestMethod;

        private static MethodInfo _conditionHandlerHasGetterMethod = null;
        private static PropertyInfo _conditionHandlerCurrentValueProperty = null;

        private static PropertyInfo _questConditionHandlersProperty;

        public static void Init()
        {
            // Get a reference to the `Localized` method for strings
            Type[] localizedParams = new Type[] { typeof(string), typeof(string) };
            Type stringLocalizeClass = PatchConstants.EftTypes.First(x => x.GetMethod("Localized", localizedParams) != null);
            _stringLocalizedMethod = AccessTools.Method(stringLocalizeClass, "Localized", localizedParams);

            _questControllerQuestsProperty = AccessTools.Property(typeof(AbstractQuestControllerClass), "Quests");
            _getQuestMethod = AccessTools.Method(_questControllerQuestsProperty.PropertyType, "GetConditional", new Type[] { typeof(string) });

            _questConditionHandlersProperty = AccessTools.Property(typeof(QuestClass), "ProgressCheckers");
        }

        public static string Localized(string input)
        {
            return (string)_stringLocalizedMethod.Invoke(null, new object[] { input, null });
        }

        public static bool ApproxEquals(float value, float value2)
        {
            return Math.Abs(value - value2) < float.Epsilon;
        }

        public static RectTransform RectTransform(GameObject gameObject)
        {
            return gameObject.transform as RectTransform;
        }

        public static RectTransform RectTransform(Component component)
        {
            return component.transform as RectTransform;
        }

        public static QuestClass GetQuest(AbstractQuestControllerClass questController, string questId)
        {
            object quests = _questControllerQuestsProperty.GetValue(questController);
            return _getQuestMethod.Invoke(quests, new object[] { questId }) as QuestClass;
        }

        public static bool ConditionHasGetter(object conditionHandler)
        {
            if (_conditionHandlerHasGetterMethod == null)
            {
                _conditionHandlerHasGetterMethod = AccessTools.Method(conditionHandler.GetType(), "HasGetter");
            }

            return (bool)_conditionHandlerHasGetterMethod.Invoke(conditionHandler, null);
        }

        public static float ConditionCurrentValue(object conditionHandler)
        {
            if (_conditionHandlerCurrentValueProperty == null)
            {
                _conditionHandlerCurrentValueProperty = AccessTools.Property(conditionHandler.GetType(), "CurrentValue");
            }

            return (float)_conditionHandlerCurrentValueProperty.GetValue(conditionHandler);
        }

        public static IDictionary GetConditionHandlers(QuestClass quest)
        {
            return _questConditionHandlersProperty.GetValue(quest) as IDictionary;
        }

        public static void DestroyAllChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        public static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            T t = gameObject.GetComponent<T>();
            if (t == null)
            {
                t = gameObject.AddComponent<T>();
            }
            return t;
        }

        public static T GetOrAddComponent<T>(MonoBehaviour component) where T : Component
        {
            return GetOrAddComponent<T>(component.gameObject);
        }
    }
}
