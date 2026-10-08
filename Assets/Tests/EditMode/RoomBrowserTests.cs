using System;
using System.Collections.Generic;
using NUnit.Framework;
using ZZabmongus.Core.Rooms;

namespace ZZabmongus.Tests
{
    public sealed class RoomBrowserTests
    {
        [TestCase(true)] [TestCase(false)]
        public void FullRoomBlocksBothVisibilities(bool isPublic)
        {
            var backend = new Directory(); backend.Data.Add(Room("full",!isPublic,4));
            using var browser = new RoomBrowserController(backend);
            browser.ClickRoom("full",2);
            Assert.That(browser.Dialog,Is.EqualTo(RoomDialog.Error));
            Assert.That(browser.Error,Is.EqualTo("방 인원이 가득 찼습니다."));
            Assert.That(backend.Joined,Is.Null);
        }
        [Test]
        public void SingleClickDoesNothing_PublicDoubleClickJoinsImmediately()
        {
            var backend = new Directory(); backend.Data.Add(Room("public"));
            using var browser = new RoomBrowserController(backend);
            browser.ClickRoom("public",1); Assert.That(backend.Joined,Is.Null);
            browser.ClickRoom("public",2); Assert.That(backend.Joined,Is.EqualTo("public"));
            Assert.That(backend.Password,Is.Empty); Assert.That(browser.Dialog,Is.EqualTo(RoomDialog.None));
        }
        [Test]
        public void PrivateDoubleClickPrompts_ThenSubmitsPassword()
        {
            var backend = new Directory(); backend.Data.Add(Room("locked",true));
            using var browser = new RoomBrowserController(backend);
            browser.ClickRoom("locked",2);
            Assert.That(browser.Dialog,Is.EqualTo(RoomDialog.Password)); Assert.That(backend.Joined,Is.Null);
            browser.SubmitPassword("1234"); Assert.That(backend.Joined,Is.EqualTo("locked")); Assert.That(backend.Password,Is.EqualTo("1234"));
        }
        [Test]
        public void RoomBecomesFullWhileTypingPassword_BlocksSubmission()
        {
            var backend = new Directory(); backend.Data.Add(Room("locked",true));
            using var browser = new RoomBrowserController(backend); browser.ClickRoom("locked",2);
            backend.Data[0] = Room("locked",true,4); backend.Notify(); browser.SubmitPassword("1234");
            Assert.That(backend.Joined,Is.Null); Assert.That(browser.Error,Is.EqualTo(RoomMessages.For(RoomDenial.Full)));
        }
        [Test]
        public void SwitchingCountriesFiltersAndClosesPasswordDialog()
        {
            var backend = new Directory(); backend.Data.Add(Room("kr",true)); backend.Data.Add(Room("jp",false,1,"JP"));
            using var browser = new RoomBrowserController(backend); browser.ClickRoom("kr",2); browser.SelectCountry("JP");
            Assert.That(browser.Dialog,Is.EqualTo(RoomDialog.None)); Assert.That(browser.Rooms.Count,Is.EqualTo(1));
            Assert.That(browser.Rooms[0].Id,Is.EqualTo("jp")); Assert.That(backend.Country,Is.EqualTo("JP"));
            browser.ClickRoom("kr",2); Assert.That(backend.Joined,Is.Null);
        }
        [Test]
        public void ServerDenialBecomesPopup()
        {
            var backend = new Directory(); using var browser = new RoomBrowserController(backend);
            backend.Reject(RoomMessages.For(RoomDenial.WrongPassword));
            Assert.That(browser.Dialog,Is.EqualTo(RoomDialog.Error)); Assert.That(browser.Error,Does.Contain("비밀번호"));
        }
        [Test]
        public void BusyBackendPreventsDuplicateActions()
        {
            var backend = new Directory { IsBusy = true }; backend.Data.Add(Room("open"));
            using var browser = new RoomBrowserController(backend); browser.OpenCreate(); browser.ClickRoom("open",2); browser.SelectCountry("JP");
            Assert.That(backend.Joined,Is.Null); Assert.That(browser.Country,Is.EqualTo("KR")); Assert.That(browser.Dialog,Is.EqualTo(RoomDialog.None));
        }
        [Test]
        public void PrivateCreationRequiresPassword_PublicCreationDropsPassword()
        {
            Assert.Throws<ArgumentException>(() => new RoomCreation("방","KR",false,""));
            var request = new RoomCreation("<b>방</b>\n","KR",true,"should not survive");
            Assert.That(request.Password,Is.Empty); Assert.That(request.Name,Does.Not.Contain("<")); Assert.That(request.Name,Does.Not.Contain("\n"));
        }
        [TestCase(3)] [TestCase(13)]
        public void InvalidCapacityCannotCreate(int capacity) => Assert.Throws<ArgumentException>(() => new RoomCreation("방","KR",true,"",capacity));
        [Test]
        public void StartedOrRemovedRoomCannotJoin()
        {
            var backend = new Directory(); backend.Data.Add(new RoomListing("started","방","KR",1,5000,false,1,4,true));
            using var browser = new RoomBrowserController(backend); browser.ClickRoom("started",2);
            Assert.That(backend.Joined,Is.Null); Assert.That(browser.Error,Is.EqualTo(RoomMessages.For(RoomDenial.InProgress)));
        }
        private static RoomListing Room(string id,bool locked=false,int players=1,string country="KR") => new(id,"테스트 방",country,1,5000,locked,players,4);
        private sealed class Directory : IRoomDirectory
        {
            public readonly List<RoomListing> Data = new(); public IReadOnlyList<RoomListing> Rooms => Data;
            public bool IsBusy { get; set; } public string Joined,Password,Country;
            public event Action Changed; public event Action<string> Failed;
            public void Refresh(string country) => Country = country;
            public void Create(RoomCreation request) { }
            public void Join(RoomListing room,string password) { Joined=room.Id; Password=password; }
            public void Notify() => Changed?.Invoke(); public void Reject(string error) => Failed?.Invoke(error);
        }
    }
    public sealed class RoomAdmissionTests
    {
        [TestCase(true)] [TestCase(false)]
        public void HostEnforcesCapacityAndReusesReleasedSeat(bool isPublic)
        {
            var gate = new RoomAdmission("room",new RoomCreation("방","KR",isPublic,"1234",4));
            for (var i=0;i<4;i++) Assert.That(gate.TryAdmit(i,"room","1234"),Is.EqualTo(RoomDenial.None));
            Assert.That(gate.TryAdmit(4,"room","1234"),Is.EqualTo(RoomDenial.Full)); Assert.That(gate.Count,Is.EqualTo(4));
            gate.Release(1); Assert.That(gate.TryAdmit(4,"room","1234"),Is.EqualTo(RoomDenial.None));
        }
        [Test]
        public void WrongPasswordDoesNotReserveSeat_CorrectPasswordDoes()
        {
            var gate = new RoomAdmission("room",new RoomCreation("방","KR",false,"정확한 비밀번호",4));
            Assert.That(gate.TryAdmit(1,"room","wrong"),Is.EqualTo(RoomDenial.WrongPassword)); Assert.That(gate.Count,Is.Zero);
            Assert.That(gate.TryAdmit(1,"room","정확한 비밀번호"),Is.EqualTo(RoomDenial.None)); Assert.That(gate.Count,Is.EqualTo(1));
        }
        [Test]
        public void DuplicatePayloadCannotConsumeExtraSeat()
        {
            var gate = new RoomAdmission("room",new RoomCreation("방","KR",true,"",4));
            for (var i=0;i<20;i++) Assert.That(gate.TryAdmit(1,"room",""),Is.EqualTo(RoomDenial.None));
            Assert.That(gate.Count,Is.EqualTo(1));
        }
        [Test]
        public void WrongRoomAndClosedOrStartedRoomAreRejected()
        {
            var gate = new RoomAdmission("room",new RoomCreation("방","KR",true,"",4));
            Assert.That(gate.TryAdmit(1,"other",""),Is.EqualTo(RoomDenial.WrongRoom)); gate.InProgress=true;
            Assert.That(gate.TryAdmit(1,"room",""),Is.EqualTo(RoomDenial.InProgress)); gate.Closed=true;
            Assert.That(gate.TryAdmit(1,"room",""),Is.EqualTo(RoomDenial.Closed)); Assert.That(gate.Count,Is.Zero);
        }
    }
}
