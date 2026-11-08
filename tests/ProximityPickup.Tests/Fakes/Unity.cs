// Fake Unity and TextMeshPro types. They hold values: they draw nothing and run no layout or physics.

using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine.UI;

namespace UnityEngine
{
    public class Object : Il2CppObjectBase
    {
        // What UnityEngine.Object.Destroy does to the == rule.
        public bool UnityDestroyed;

        private static bool IsGone(Object o) => ReferenceEquals(o, null) || o.UnityDestroyed;

        public static bool operator ==(Object a, Object b)
        {
            if (IsGone(a) || IsGone(b)) return IsGone(a) && IsGone(b);
            return ReferenceEquals(a, b);
        }

        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => other is Object o && this == o;
        public override int GetHashCode() => Pointer.GetHashCode();
    }

    public class GameObject : Object
    {
        private readonly List<Component> components = new List<Component>();

        public readonly string name;
        public bool activeSelf = true;
        public Transform transform { get; private set; }

        public GameObject(string name = null)
        {
            this.name = name;
            AddComponent<Transform>();
        }

        public bool activeInHierarchy => activeSelf && (transform.parent is null || transform.parent.gameObject.activeInHierarchy);

        public void SetActive(bool value) { activeSelf = value; }

        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            if (component is Transform t)
            {
                components.Remove(transform);
                transform = t;
            }
            components.Add(component);
            return component;
        }

        public T GetComponent<T>() where T : class
        {
            foreach (var component in components)
                if (component is T found) return found;
            return null;
        }

        internal void Collect<T>(List<T> found, bool includeInactive) where T : class
        {
            if (!includeInactive && !activeInHierarchy) return;
            foreach (var component in components)
                if (component is T match) found.Add(match);
            foreach (var child in transform.children) child.gameObject.Collect(found, includeInactive);
        }
    }

    public class Component : Object
    {
        public GameObject gameObject { get; internal set; }
        public Transform transform => gameObject.transform;

        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();

        public Il2CppArrayBase<T> GetComponentsInChildren<T>(bool includeInactive) where T : class
        {
            var found = new List<T>();
            gameObject.Collect(found, includeInactive);
            return new Il2CppArrayBase<T>(found);
        }
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
    }

    public class Transform : Component
    {
        private Vector3 worldPosition;
        private Vector3? ownScale;

        public readonly List<Transform> children = new List<Transform>();
        public Transform parent { get; private set; }
        public Vector3 localPosition;

        // The count of writes, so a test can prove that a frame wrote nothing.
        public int PositionWrites;

        public Vector3 position
        {
            get => worldPosition;
            set { worldPosition = value; PositionWrites++; }
        }

        public Vector3 lossyScale
        {
            get => ownScale ?? parent?.lossyScale ?? Vector3.one;
            set => ownScale = value;
        }

        public void SetParent(Transform newParent, bool worldPositionStays)
        {
            parent?.children.Remove(this);
            parent = newParent;
            parent?.children.Add(this);
        }

        public void SetAsFirstSibling()
        {
            parent.children.Remove(this);
            parent.children.Insert(0, this);
        }

        public Vector3 TransformPoint(Vector3 p)
        {
            var s = lossyScale;
            return new Vector3(position.x + p.x * s.x, position.y + p.y * s.y, position.z + p.z * s.z);
        }
    }

    public class RectTransform : Transform
    {
        public Rect rect;
        public Vector2 pivot, anchorMin, anchorMax, sizeDelta, anchoredPosition, offsetMin, offsetMax;
    }

    public struct Rect
    {
        public float x, y, width, height;

        public Rect(float x, float y, float width, float height)
        {
            this.x = x; this.y = y; this.width = width; this.height = height;
        }

        public float xMin => x;
        public float yMin => y;
        public float xMax => x + width;
        public float yMax => y + height;
    }

    public struct Bounds
    {
        public Vector3 center, size;

        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center; this.size = size;
        }
    }

    public struct Vector2
    {
        public float x, y;

        public Vector2(float x, float y)
        {
            this.x = x; this.y = y;
        }

        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 one => new Vector2(1f, 1f);

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);

        public static bool operator ==(Vector2 a, Vector2 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return dx * dx + dy * dy < 1e-5f * 1e-5f;
        }

        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public override bool Equals(object other) => other is Vector2 v && this == v;
        public override int GetHashCode() => (x, y).GetHashCode();
    }

    public struct Vector3
    {
        public float x, y, z;

        public Vector3(float x, float y, float z)
        {
            this.x = x; this.y = y; this.z = z;
        }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);

        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static bool operator ==(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return dx * dx + dy * dy + dz * dz < 1e-5f * 1e-5f;
        }

        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object other) => other is Vector3 v && this == v;
        public override int GetHashCode() => (x, y, z).GetHashCode();
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2})";
    }

    public struct Vector4
    {
        public float x, y, z, w;

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x; this.y = y; this.z = z; this.w = w;
        }
    }

    public struct Color
    {
        public float r, g, b, a;

        public Color(float r, float g, float b, float a)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }

        public static bool operator ==(Color p, Color q) => p.r == q.r && p.g == q.g && p.b == q.b && p.a == q.a;
        public static bool operator !=(Color p, Color q) => !(p == q);
        public override bool Equals(object other) => other is Color c && this == c;
        public override int GetHashCode() => (r, g, b, a).GetHashCode();
    }

    public static class ColorUtility
    {
        private static int Byte(float value) => (int)Math.Round(Math.Clamp(value, 0f, 1f) * 255f);

        public static string ToHtmlStringRGB(Color c) => $"{Byte(c.r):X2}{Byte(c.g):X2}{Byte(c.b):X2}";
    }

    public static class Screen
    {
        public static int width, height;
    }

    public static class Time
    {
        public static int frameCount;
    }

    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }

    public class Rigidbody : Component { }

    public class Collider : Component
    {
        public Rigidbody attachedRigidbody;
    }

    public static class Physics
    {
        public static readonly List<Collider> Hits = new List<Collider>();
        public static int Calls;
        public static (Vector3 Position, float Radius, int LayerMask, QueryTriggerInteraction Query) LastCall;

        public static Il2CppArrayBase<Collider> OverlapSphere(Vector3 position, float radius, int layerMask, QueryTriggerInteraction query)
        {
            Calls++;
            LastCall = (position, radius, layerMask, query);
            return new Il2CppArrayBase<Collider>(Hits);
        }
    }

    public class CanvasGroup : Behaviour
    {
        public bool blocksRaycasts = true;
    }
}

namespace UnityEngine.UI
{
    public class Graphic : Behaviour
    {
        public Color color = new Color(1f, 1f, 1f, 1f);
        public bool raycastTarget = true;
        public Vector4 raycastPadding;
        public RectTransform rectTransform => GetComponent<RectTransform>();
    }

    public class Image : Graphic { }

    public class LayoutElement : Behaviour
    {
        public bool ignoreLayout;
    }

    public class LayoutGroup : Behaviour { }

    public static class LayoutRebuilder
    {
        public static readonly List<RectTransform> Rebuilt = new List<RectTransform>();

        public static void ForceRebuildLayoutImmediate(RectTransform rect) { Rebuilt.Add(rect); }
    }
}

namespace TMPro
{
    public class TextMeshProUGUI : UnityEngine.UI.Graphic
    {
        public string text;
        public float fontSize;
        public UnityEngine.Bounds textBounds;
        public int Measures, MeshUpdates;

        // One text unit is 10 by 20 canvas units. A test can replace the measure.
        public Func<string, UnityEngine.Vector2> Measure = s => new UnityEngine.Vector2(s.Length * 10f, 20f);

        public UnityEngine.Vector2 GetPreferredValues(string value)
        {
            Measures++;
            return Measure(value);
        }

        public void ForceMeshUpdate(bool ignoreActiveState, bool forceTextReparsing) { MeshUpdates++; }
    }
}
