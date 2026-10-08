using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZZabmongus.Core.Rooms
{
    public static class Countries
    {
        public static readonly string[] Codes = { "KR", "JP", "SG", "US", "DE", "AU" };
        public static readonly string[] Names = { "대한민국", "일본", "싱가포르", "미국", "독일", "호주" };
        public static bool Contains(string code) => Array.IndexOf(Codes, code) >= 0;
        public static string Name(string code) { var index = Array.IndexOf(Codes, code); return index >= 0 ? Names[index] : code; }
    }

    public enum RoomDenial : byte { None, Full, WrongPassword, Closed, WrongRoom, InProgress, InvalidRequest }
    public static class RoomMessages
    {
        public static string For(RoomDenial reason) => reason switch {
            RoomDenial.Full => "방 인원이 가득 찼습니다.",
            RoomDenial.WrongPassword => "비밀번호가 올바르지 않습니다.",
            RoomDenial.InProgress => "이미 게임이 시작된 방입니다.",
            RoomDenial.WrongRoom => "방 정보가 변경되었습니다. 목록을 새로고침해 주세요.",
            RoomDenial.InvalidRequest => "입장 요청이 올바르지 않습니다.",
            _ => "방이 종료되었거나 연결할 수 없습니다."
        };
    }

    public sealed class RoomCreation
    {
        public string Name { get; }
        public string Country { get; }
        public bool IsPublic { get; }
        public string Password { get; }
        public int Capacity { get; }
        public RoomCreation(string name, string country, bool isPublic, string password, int capacity = 12)
        {
            Name = CleanName(name);
            if (Name.Length == 0) throw new ArgumentException("방 이름을 입력해 주세요.");
            if (!Countries.Contains(country)) throw new ArgumentException("서버를 선택해 주세요.");
            if (capacity < 4 || capacity > 12) throw new ArgumentException("방 인원은 4~12명으로 설정해 주세요.");
            if (!isPublic && string.IsNullOrEmpty(password)) throw new ArgumentException("비공개 방의 비밀번호를 입력해 주세요.");
            if (!isPublic && password.Length > 32) throw new ArgumentException("비밀번호는 32자 이내로 입력해 주세요.");
            Country = country; IsPublic = isPublic; Password = isPublic ? "" : password; Capacity = capacity;
        }
        public static string CleanName(string value)
        {
            var result = new StringBuilder();
            foreach (var c in value ?? "")
            {
                if (result.Length >= 40) break;
                if (!char.IsControl(c) && !char.IsSurrogate(c) && c != '<' && c != '>') result.Append(c);
            }
            return result.ToString().Trim();
        }
    }

    /// <summary>Public discovery data. Never put passwords, hashes or salts here.</summary>
    public sealed class RoomListing
    {
        public string Id { get; }
        public string Name { get; }
        public string Country { get; }
        public ulong Host { get; }
        public ushort Port { get; }
        public bool IsPrivate { get; }
        public int Players { get; }
        public int Capacity { get; }
        public bool InProgress { get; }
        public bool IsFull => Players >= Capacity;
        public RoomListing(string id, string name, string country, ulong host, ushort port, bool isPrivate, int players, int capacity, bool inProgress = false)
        {
            Id = id; Name = RoomCreation.CleanName(name); Country = country; Host = host; Port = port;
            IsPrivate = isPrivate; Players = players; Capacity = capacity; InProgress = inProgress;
        }
    }

    public interface IRoomDirectory
    {
        IReadOnlyList<RoomListing> Rooms { get; }
        bool IsBusy { get; }
        event Action Changed;
        event Action<string> Failed;
        void Refresh(string country);
        void Create(RoomCreation request);
        void Join(RoomListing room, string password);
    }

    public enum RoomDialog { None, Create, Password, Error }

    /// <summary>UI flow kept independent of Steam to verify all join decisions.</summary>
    public sealed class RoomBrowserController : IDisposable
    {
        private readonly IRoomDirectory directory;
        private string selectedRoom;
        public string Country { get; private set; } = "KR";
        public RoomDialog Dialog { get; private set; }
        public string Error { get; private set; }
        public event Action Changed;
        public IReadOnlyList<RoomListing> Rooms => directory.Rooms.Where(r => r.Country == Country).ToArray();
        public RoomListing SelectedRoom => Rooms.FirstOrDefault(r => r.Id == selectedRoom);
        public bool Busy => directory.IsBusy;
        public RoomBrowserController(IRoomDirectory value)
        {
            directory = value;
            directory.Changed += DirectoryChanged;
            directory.Failed += ShowError;
        }
        public void SelectCountry(string code)
        {
            if (Busy || !Countries.Contains(code)) return;
            Country = code; CloseDialog(); directory.Refresh(code);
        }
        public void Refresh() { if (!Busy) directory.Refresh(Country); }
        public void OpenCreate() { if (!Busy) { Dialog = RoomDialog.Create; Changed?.Invoke(); } }
        public void Create(string name, bool isPublic, string password, int capacity)
        {
            if (Busy) return;
            try { var request = new RoomCreation(name, Country, isPublic, password, capacity); CloseDialog(); directory.Create(request); }
            catch (ArgumentException error) { ShowError(error.Message); }
        }
        public void ClickRoom(string id, int clickCount)
        {
            if (clickCount != 2 || Busy || Dialog != RoomDialog.None) return;
            selectedRoom = id;
            var room = SelectedRoom;
            if (!CanJoin(room)) return;
            if (room.IsPrivate) { Dialog = RoomDialog.Password; Changed?.Invoke(); }
            else directory.Join(room, "");
        }
        public void SubmitPassword(string password)
        {
            if (Dialog != RoomDialog.Password || Busy) return;
            var room = SelectedRoom;
            if (!CanJoin(room)) return;
            if (string.IsNullOrEmpty(password)) { ShowError("비밀번호를 입력해 주세요."); return; }
            CloseDialog(); directory.Join(room, password);
        }
        private bool CanJoin(RoomListing room)
        {
            if (room == null) { ShowError(RoomMessages.For(RoomDenial.Closed)); return false; }
            if (room.IsFull) { ShowError(RoomMessages.For(RoomDenial.Full)); return false; }
            if (room.InProgress) { ShowError(RoomMessages.For(RoomDenial.InProgress)); return false; }
            return true;
        }
        public void CloseDialog() { Dialog = RoomDialog.None; Error = ""; Changed?.Invoke(); }
        public void ShowError(string message) { Error = message; Dialog = RoomDialog.Error; Changed?.Invoke(); }
        private void DirectoryChanged() => Changed?.Invoke();
        public void Dispose() { directory.Changed -= DirectoryChanged; directory.Failed -= ShowError; }
    }
}
