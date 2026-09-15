namespace FlowNote.Core;

/// <summary>
/// PHASE 00에서 고정하는 제품 어휘와 금지 규칙.
/// 이후 단계가 데이터 모델과 화면을 채울 때 이 값을 뒤집지 않는다.
/// </summary>
public static class ProductInvariants
{
    public const string WorkItemTypeName = "WorkItem";

    public static bool CompletionDeletesWorkItem => false;

    public static bool TreatsRecordedAtAsWorkStart => false;

    public static bool TreatsRecordedIntervalAsWorkDuration => false;
}
