using System;
using UnityEngine;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Drawn parts of the avatars (spec «Профиль, аватар и семья»): the bodies, the 12
    /// ready faces, and where on each body a face goes. Lives in Resources so every
    /// screen can draw an avatar; created by AvatarScreenBuilder from the stand-in art.
    ///
    /// Rects are normalized in the body sprite: centre x, centre y from the TOP, width,
    /// height. The final art should come as bodies without faces; with the stand-ins the
    /// photo oval simply covers the drawn face.
    /// </summary>
    [CreateAssetMenu(menuName = "Nooria/Avatar Art")]
    public class AvatarArt : ScriptableObject
    {
        [Serializable]
        public class Body
        {
            public Sprite sprite;
            /// <summary>The face oval, where a photo goes (its height; width follows the photo).</summary>
            public Rect face = new(0.5f, 0.2f, 0.3f, 0.2f);
            /// <summary>The whole head with hair or scarf: a drawn face covers it, a bust zooms to it.</summary>
            public Rect head = new(0.5f, 0.18f, 0.5f, 0.34f);
        }

        public const string ResourcePath = "LessonAvatarArt";

        public Body girl = new();
        public Body boy = new();
        public Body mom = new();
        /// <summary>Ready drawn faces (whole heads) the child can pick instead of a photo.</summary>
        public Sprite[] faces = Array.Empty<Sprite>();
        /// <summary>Circle for busts.</summary>
        public Sprite circle;

        private static AvatarArt _instance;
        public static AvatarArt Instance => _instance != null ? _instance : _instance = Resources.Load<AvatarArt>(ResourcePath);

        public Body BodyFor(AvatarPerson person) => person switch
        {
            AvatarPerson.Mom => mom,
            _ => UserProfile.ChildGender == "female" ? girl : boy,
        };

        public Sprite Face(int index) => index >= 0 && index < faces.Length ? faces[index] : null;
    }
}
