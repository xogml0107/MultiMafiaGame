using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ZZabmongus.Core.Rooms
{
    /// <summary>Host-only seat reservations. Validate and reserve in one synchronous operation.</summary>
    public sealed class RoomAdmission
    {
        private readonly HashSet<int> admitted = new();
        private readonly byte[] salt, passwordHash;
        public string RoomId { get; }
        public string Name { get; }
        public string Country { get; }
        public bool IsPrivate { get; }
        public int Capacity { get; }
        public int Count => admitted.Count;
        public bool Closed { get; set; }
        public bool InProgress { get; set; }
        public RoomAdmission(string id, RoomCreation settings)
        {
            RoomId = id; Name = settings.Name; Country = settings.Country; IsPrivate = !settings.IsPublic; Capacity = settings.Capacity;
            if (!settings.IsPublic)
            {
                salt = new byte[16];
                using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
                passwordHash = Hash(settings.Password);
            }
        }
        public RoomDenial TryAdmit(int connection, string roomId, string password)
        {
            if (Closed) return RoomDenial.Closed;
            if (RoomId != roomId) return RoomDenial.WrongRoom;
            if (admitted.Contains(connection)) return RoomDenial.None;
            if (InProgress) return RoomDenial.InProgress;
            if (Count >= Capacity) return RoomDenial.Full;
            if (IsPrivate)
            {
                if (password == null || password.Length > 32) return RoomDenial.WrongPassword;
                var supplied = Hash(password);
                var difference = 0;
                for (var i = 0; i < supplied.Length; i++) difference |= supplied[i] ^ passwordHash[i];
                if (difference != 0) return RoomDenial.WrongPassword;
            }
            admitted.Add(connection);
            return RoomDenial.None;
        }
        public bool Release(int connection) => admitted.Remove(connection);
        private byte[] Hash(string password)
        {
            using var derive = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt, 20000, HashAlgorithmName.SHA256);
            return derive.GetBytes(32);
        }
    }
}
