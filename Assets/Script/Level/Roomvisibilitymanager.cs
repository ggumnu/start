using System.Collections.Generic;
using UnityEngine;

// 씬 전체에서 "지금 플레이어가 있는 방의 내용물만" 보이도록 관리합니다.
// 방의 트리거(감지용 콜라이더)는 항상 켜져 있고, 이 매니저는 각 방의
// "내용물(바닥/벽/게이트/적)"만 켜고 끕니다. 그래야 꺼진 방도 나중에
// 다시 트리거로 감지되어 켜질 수 있습니다. (트리거까지 같이 꺼버리면
// 다시는 그 방을 감지할 수 없게 되어버립니다.)
public class RoomVisibilityManager : MonoBehaviour
{
    private readonly List<GameObject> allRoomContents = new List<GameObject>();
    private GameObject current;

    public void Register(GameObject roomContent)
    {
        allRoomContents.Add(roomContent);
        roomContent.SetActive(false); // 처음엔 전부 꺼둡니다.
    }

    public void ShowOnly(GameObject roomContent)
    {
        if (current == roomContent) return;
        current = roomContent;

        foreach (var content in allRoomContents)
        {
            content.SetActive(content == roomContent);
        }
    }
}