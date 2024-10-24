using UnityEngine;
using UniverseLib.UI.ObjectPool;

namespace UniverseLib.UI.Widgets.ScrollView
{
    public interface ICell : IPooledObject
    {
        bool Enabled { get; }

        RectTransform Rect { get; set; }

        void Enable();
        void Disable();
    }
}
