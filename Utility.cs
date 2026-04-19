using UnityEngine;

namespace Patty_CardPicker_MOD
{
    internal static class Utility
    {
        public static T AddOrGetComponent<T>(this GameObject obj) where T : Component
        {
            var component = obj.GetComponent<T>();
            if (component == null)
            {
                component = obj.AddComponent<T>();
            }
            return component;
        }

        public static void DestroyGameObject(this Transform transform, string name)
        {
            var child = transform.Find(name);
            if (child != null)
            {
                GameObject.DestroyImmediate(child.gameObject);
            }
        }
    }
}
