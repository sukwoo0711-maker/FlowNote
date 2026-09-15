# V3 요구-코드-검수 매핑

| 요구 | 코드 | 검수 |
|---|---|---|
| F01 캡슐 520×52, 성공 분리 | `FloatingCapsuleWindow`, `CapsuleInputBar`, `SuccessFlash` | 기존 캡슐 테스트 + smoke chrome |
| F01 업무 칩·초안 연결 | `FloatingViewModel.DraftWorkItemId`, `drafts.work_item_id` | `WorkLinkPersistenceTests`, V3-14~18 일부 |
| W02 업무 맥락 | `WorkContextQuery`, `WorkContextPane` | `V3ScenarioAndReportTests` + smoke `v3-work-context` |
| N01 다음 행동 | migration 2, `SetNextActionAsync`, `work_item_revisions` | `NextActionPersistenceTests`, `NextActionRulesTests` |
| T01 타임라인 | `MainWindow`, `TimelineEntryCard`, `이어서 할 일` | smoke 메인 캡처 |
| R01 선택 보고 | `ReportBuilder`, `DayReportWindow`, `ReportZipWriter` | `ReportBuilderTests`, `V3ScenarioAndReportTests` |
| O02 샘플 하루 | `V3ScenarioSeeder`, `--demo` 1회 시딩, 게시본 `fixtures/v3` | 시나리오 단위 테스트. `--demo` GUI 완주는 NOT RUN. 운영 DB 미시딩 |
| 완료 일관성 | `MutateAsync` + next action clear | `NextActionPersistenceTests` |
| 백업 | `SqliteBackupService` | `Backup_restores_next_action` |
| 파노라마 | 미구현 안내 유지 | 구현 아님으로 보고 |

스키마 변경: `work_items.next_action_*` 4열, `work_item_revisions.request_id` + 부분 고유 인덱스. version 2.
