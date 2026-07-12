using System;

namespace FairyTales.Models
{
    [Serializable]
    public class LoginRequest
    {
        public string userId;
    }

    [Serializable]
    public class RegisterRequest
    {
        public string userId;
        public string name;
        public string gender;
        public string lang;
    }

    [Serializable]
    public class LoginResponse
    {
        public string token;
    }

    [Serializable]
    public class RegisterResponse
    {
        public string token;
        public ProfileData profile;
    }

    [Serializable]
    public class ProfileData
    {
        public string user_id;
        public string name;
        public string gender;
        public string lang;
    }

    [Serializable]
    public class ProfileUpdateRequest
    {
        public string name;
        public string gender;
        public string lang;
    }

    [Serializable]
    public class ProfileUpdateResponse
    {
        public ProfileData profile;
    }
}
