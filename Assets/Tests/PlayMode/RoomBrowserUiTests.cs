#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZZabmongus.Core.Rooms;
using ZZabmongus.Networking;

namespace ZZabmongus.Tests
{
    public sealed class RoomBrowserUiTests
    {
        [UnityTest]
        public IEnumerator BrowserRendersLocks_DoubleClicks_PasswordToggle_FullPopup_AndCountryFilter()
        {
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/MultiplayerBase.unity",new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            var browser = UnityEngine.Object.FindFirstObjectByType<RoomBrowserHud>();
            var directory = new Directory();
            directory.Data.Add(new RoomListing("open","공개 테스트 방","KR",1,5000,false,1,4));
            directory.Data.Add(new RoomListing("locked","비공개 테스트 방","KR",2,5000,true,1,4));
            directory.Data.Add(new RoomListing("full","가득 찬 방","KR",3,5000,true,4,4));
            directory.Data.Add(new RoomListing("japan","일본 방","JP",4,5000,false,1,4));
            browser.Bind(directory); yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(UnityEngine.Object.FindObjectsByType<RoomRow>(FindObjectsSortMode.None).Length,Is.EqualTo(3));
            var row = UnityEngine.Object.FindObjectsByType<RoomRow>(FindObjectsSortMode.None).Single(r => r.Listing.Id == "locked");
            Assert.That(row.transform.Find("LockIcon").gameObject.activeSelf,Is.True);
            if (!Application.isBatchMode)
            {
                System.IO.Directory.CreateDirectory("Logs"); ScreenCapture.CaptureScreenshot("Logs/room-browser-main.png");
                yield return new WaitForSecondsRealtime(0.3f);
            }
            row.OnPointerClick(new PointerEventData(EventSystem.current) { clickCount=1 }); Assert.That(browser.Controller.Dialog,Is.EqualTo(RoomDialog.None));
            row.OnPointerClick(new PointerEventData(EventSystem.current) { clickCount=2 }); Assert.That(browser.Controller.Dialog,Is.EqualTo(RoomDialog.Password));
            Assert.That(GameObject.Find("PasswordRoomDialog"),Is.Not.Null);
            var password = browser.GetComponentsInChildren<TMP_InputField>(true).Single(f => f.name == "JoinRoomPassword");
            Assert.That(password.contentType,Is.EqualTo(TMP_InputField.ContentType.Password));
            password.text = "1234"; GameObject.Find("ConfirmJoinPassword").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(directory.Joined,Is.EqualTo("locked")); Assert.That(directory.Password,Is.EqualTo("1234")); Assert.That(password.text,Is.Empty);
            var full = UnityEngine.Object.FindObjectsByType<RoomRow>(FindObjectsSortMode.None).Single(r => r.Listing.Id == "full");
            full.OnPointerClick(new PointerEventData(EventSystem.current) { clickCount=2 });
            Assert.That(GameObject.Find("RoomErrorDialog"),Is.Not.Null); Assert.That(browser.Controller.Error,Is.EqualTo("방 인원이 가득 찼습니다."));
            Assert.That(directory.Joined,Is.EqualTo("locked"),"Full room must not call the join backend");
            var error = GameObject.Find("RoomErrorDialog").GetComponentsInChildren<TMP_Text>().Single(t => t.text.Contains("가득"));
            Assert.That(error.font.HasCharacter('방',true),Is.True,"Korean UI glyphs must render");
            if (!Application.isBatchMode)
            {
                ScreenCapture.CaptureScreenshot("Logs/room-browser-full.png"); yield return new WaitForSecondsRealtime(0.3f);
            }
            browser.Controller.CloseDialog();
            GameObject.Find("OpenCreateRoom").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            var toggle = GameObject.Find("PublicRoomToggle").GetComponent<UnityEngine.UI.Toggle>();
            var input = GameObject.Find("CreateRoomPassword").GetComponent<TMP_InputField>();
            Assert.That(input.interactable,Is.False); toggle.isOn=false; Assert.That(input.interactable,Is.True);
            input.text="private"; toggle.isOn=true; Assert.That(input.interactable,Is.False); Assert.That(input.text,Is.Empty);
            Canvas.ForceUpdateCanvases();
            var panel = (RectTransform)GameObject.Find("CreateRoomDialog").transform;
            var confirm = (RectTransform)GameObject.Find("ConfirmCreateRoom").transform;
            var corners = new Vector3[4]; confirm.GetWorldCorners(corners);
            Assert.That(panel.InverseTransformPoint(corners[0]).y,Is.GreaterThanOrEqualTo(panel.rect.yMin));
            browser.Controller.CloseDialog();
            GameObject.Find("Server_JP").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
            Assert.That(browser.Controller.Rooms.Count,Is.EqualTo(1)); Assert.That(browser.Controller.Rooms[0].Id,Is.EqualTo("japan"));
            var japanese = UnityEngine.Object.FindObjectsByType<RoomRow>(FindObjectsSortMode.None).Single();
            Assert.That(japanese.transform.Find("LockIcon").gameObject.activeSelf,Is.False);
            japanese.OnPointerClick(new PointerEventData(EventSystem.current) { clickCount=2 });
            Assert.That(directory.Joined,Is.EqualTo("japan")); Assert.That(browser.Controller.Dialog,Is.EqualTo(RoomDialog.None));
            LogAssert.NoUnexpectedReceived();
        }
        private sealed class Directory : IRoomDirectory
        {
            public List<RoomListing> Data = new(); public IReadOnlyList<RoomListing> Rooms => Data;
            public bool IsBusy => false; public string Joined,Password;
            public event Action Changed; public event Action<string> Failed;
            public void Refresh(string country) => Changed?.Invoke();
            public void Create(RoomCreation request) { }
            public void Join(RoomListing room,string password) { Joined=room.Id; Password=password; }
        }
    }
}
#endif
