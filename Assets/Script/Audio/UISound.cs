// UI 버튼 소리 공용 도우미입니다. 각 창(팝업/중단 화면/상점 등)은 버튼 위 마우스 판정을 직접 하므로, 판정한 결과를 여기에 넘깁니다.
// - 진행(예/계속 등) = Confirm, 취소(아니오/뒤로/닫기, Esc) = Cancel, 위험 버튼(재시작/그만하기) = Danger.
// - 호버 = TrackHover: 마우스가 "다른 버튼 위로 새로 올라간" 순간에만 한 번 납니다(같은 버튼 위에 머물면 반복 X).
public static class UISound
{
    public static void Confirm() => GameAudio.Play(SfxId.UIConfirm);
    public static void Cancel() => GameAudio.Play(SfxId.UICancel);
    public static void Danger() => GameAudio.Play(SfxId.UIDanger);

    // hoveredId = 지금 마우스가 올라간 버튼 번호(-1 = 없음). 창마다 버튼 번호가 겹치지 않게 정해서 넘깁니다.
    // silent = 창이 막 열린 프레임 등 소리 없이 기억만 맞출 때(이미 버튼 위에 있던 마우스로 창이 뜨면 소리 X).
    public static void TrackHover(ref int lastHoveredId, int hoveredId, bool silent = false)
    {
        if (!silent && hoveredId >= 0 && hoveredId != lastHoveredId) GameAudio.Play(SfxId.UIHover);
        lastHoveredId = hoveredId;
    }

    // 버튼 마우스 판정 여러 개 중 처음으로 true인 것의 번호 + idOffset(없으면 -1)입니다.
    public static int HoveredId(int idOffset, bool b0, bool b1 = false, bool b2 = false, bool b3 = false, bool b4 = false)
    {
        if (b0) return idOffset;
        if (b1) return idOffset + 1;
        if (b2) return idOffset + 2;
        if (b3) return idOffset + 3;
        if (b4) return idOffset + 4;
        return -1;
    }
}
