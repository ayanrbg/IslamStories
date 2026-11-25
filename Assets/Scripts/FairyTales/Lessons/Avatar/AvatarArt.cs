using System;
using UnityEngine;

namespace FairyTales.Lessons
{
    /// <summary>
    /// Drawn parts of the avatars, Speech Blubs style: bodies WITHOUT heads, and heads that
    /// sit on them — a drawn one, or the child's real head cut out of a photo. Lives in
    /// Resources so every screen can draw an avatar; filled by AvatarScreenBuilder from
    /// Assets/UI/Lessons/Avatar2D (parts.json has the measured anchors).
    ///
    /// Coordinates are normalized in the sprite, y from the TOP. Sizes are shares of the
    /// body's width: heads are drawn big on purpose (the funny paper-doll look).
    /// </summary>
    [CreateAssetMenu(menuName = "Nooria/Avatar Art")]
    public class AvatarArt : ScriptableObject
    {
        [Serializable]
        public class Body
        {
            public string id;
            public Sprite sprite;
            /// <summary>Optional layer over the head (a collar, a scarf's front) for the final art.</summary>
            public Sprite front;
            /// <summary>Where the chin goes: the top of the neck.</summary>
            public Vector2 neck = new(0.5f, 0f);
            /// <summary>Width of a drawn head.</summary>
            public float headWidth = 1f;
            /// <summary>Width of a photo head's FACE (cheek to cheek) — its hair comes on top.</summary>
            public float faceWidth = 0.62f;
        }

        [Serializable]
        public class Head
        {
            public Sprite sprite;
            /// <summary>Bottom of the chin.</summary>
            public Vector2 chin = new(0.5f, 0.95f);
            /// <summary>Wears a hijab: goes on the body with the drawn scarf drape.</summary>
            public bool hijab;
        }

        public const string ResourcePath = "LessonAvatarArt";

        public Body boy = new();
        public Body girl = new();
        public Body girlHijab = new();
        public Body mom = new();
        /// <summary>The 12 ready heads the child can pick instead of a photo (girls first).</summary>
        public Head[] kidHeads = Array.Empty<Head>();
        /// <summary>Ready heads for mum.</summary>
        public Head[] momHeads = Array.Empty<Head>();
        /// <summary>Circle for busts.</summary>
        public Sprite circle;

        private static AvatarArt _instance;
        public static AvatarArt Instance => _instance != null ? _instance : _instance = Resources.Load<AvatarArt>(ResourcePath);

        public Head[] HeadsFor(AvatarPerson person) => person == AvatarPerson.Mom ? momHeads : kidHeads;

        public Head HeadFor(AvatarPerson person, int index)
        {
            var heads = HeadsFor(person);
            return heads.Length == 0 ? null : heads[Mathf.Clamp(index, 0, heads.Length - 1)];
        }

        /// <summary>The head shown when nothing was chosen: a girl in a hijab, a boy, mum.</summary>
        public int DefaultHead(AvatarPerson person)
        {
            if (person == AvatarPerson.Mom) return 0;
            bool girlChild = UserProfile.ChildGender == "female";
            for (int i = 0; i < kidHeads.Length; i++)
                if (kidHeads[i].hijab == girlChild && (girlChild || i >= kidHeads.Length / 2)) return i;
            return 0;
        }

        /// <summary>
        /// The body for a person: mum's own; the child's by gender — a girl in a hijab (drawn
        /// head with a hijab, or a covered photo) stands on the body with the scarf drape,
        /// unless the parent picked the other body.
        /// </summary>
        public Body BodyFor(AvatarPerson person, AvatarRecord record)
        {
            if (person == AvatarPerson.Mom) return mom;
            if (UserProfile.ChildGender != "female") return boy;
            if (record?.body == "girl") return girl;
            if (record?.body == "girl_hijab") return girlHijab;
            bool hijab = record != null && record.kind == AvatarStore.KindPhoto
                ? record.covered
                : HeadFor(person, record != null && record.kind == AvatarStore.KindFace ? record.face : DefaultHead(person))?.hijab ?? true;
            return hijab ? girlHijab : girl;
        }
    }
}
