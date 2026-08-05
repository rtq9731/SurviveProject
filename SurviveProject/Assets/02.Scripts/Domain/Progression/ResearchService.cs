using Survive.Items;

namespace Survive.Progression
{
    /// <summary>연구를 지금 걸 수 있는가, 없다면 왜 없는가.</summary>
    public enum ResearchReadiness
    {
        Ready = 0,

        /// <summary>항목이 없거나, 열 것이 하나도 없거나, 태울 것을 지정하지 않았다.</summary>
        Invalid,

        /// <summary>이미 다 알아냈다. 두 번 알아낼 것은 없다.</summary>
        AlreadyKnown,

        /// <summary>이미 줄에 서 있다.</summary>
        Queued,

        /// <summary>줄이 가득 찼다.</summary>
        QueueFull,

        /// <summary>들여다볼 물건이 없다.</summary>
        MissingMaterial,

        /// <summary>태울 스크랩이 모자란다.</summary>
        MissingEnergy,
    }

    /// <summary>
    /// 연구의 생애 규칙 — 걸고, 들여다보고, 물린다.
    ///
    /// <see cref="Survive.Crafting.CraftQueueService"/>가 제작에 대해 하는 일을 연구에
    /// 대해 한다. 규칙도 일부러 같게 맞췄다: <b>재료는 걸 때 전부 빠지고</b>,
    /// <b>끝나지 않은 것을 물리면 전부 돌아온다</b>. 제작대와 연구대에서 취소가 다르게
    /// 굴면 그것은 두 개의 게임이다.
    ///
    /// 다른 것은 완성의 의미 하나뿐이다 — 제작은 회수함에 물건을 놓고, 연구는
    /// <see cref="UnlockLedger"/>에 줄을 적는다.
    ///
    /// 순수 정적이라 Unity 실행 없이 테스트한다.
    /// </summary>
    public static class ResearchService
    {
        /// <summary>
        /// 태울 것의 기본 이름. 연구 목록 에셋이 아이템을 지정하지 않았을 때 무엇을
        /// 찾아야 하는지 한 곳에만 적어 둔다.
        /// </summary>
        public const string DefaultEnergyItemId = "scrap";

        public static string EnergyIdOf(ItemDataSO energy) =>
            energy != null && !string.IsNullOrEmpty(energy.id) ? energy.id : DefaultEnergyItemId;

        /// <summary>
        /// 이 항목이 여는 것이 하나라도 있는가.
        ///
        /// 산출물은 두 갈래다 — 청사진(<see cref="ResearchEntrySO.unlocks"/>)과
        /// 청사진을 거치지 않는 원장 열쇠(<see cref="ResearchEntrySO.unlockKeys"/>).
        /// 둘 다 비어 있으면 걸어 봐야 아무 일도 일어나지 않는 헛 항목이다.
        /// </summary>
        public static bool HasAnyUnlock(ResearchEntrySO entry)
        {
            if (entry == null) return false;

            if (entry.unlocks != null)
                foreach (var bp in entry.unlocks)
                    if (bp != null && !string.IsNullOrWhiteSpace(bp.id)) return true;

            if (entry.unlockKeys != null)
                foreach (var key in entry.unlockKeys)
                    if (!string.IsNullOrWhiteSpace(key)) return true;

            return false;
        }

        /// <summary>
        /// 이 항목이 여는 것을 전부 이미 알고 있는가.
        ///
        /// "연구했다"는 사실을 따로 세어 두지 않는다. 알아낸 결과가 곧 원장에 있고,
        /// 세는 자리가 둘이면 언젠가 둘이 어긋난다 —
        /// <see cref="FieldDiscovery"/>가 첫 습득을 세지 않는 것과 같은 이유다.
        /// </summary>
        public static bool IsKnown(ResearchEntrySO entry, UnlockLedger ledger)
        {
            if (!HasAnyUnlock(entry)) return false;
            if (ledger == null) return false;

            if (entry.unlocks != null)
            {
                foreach (var bp in entry.unlocks)
                {
                    if (bp == null || string.IsNullOrWhiteSpace(bp.id)) continue;
                    if (!ledger.IsUnlocked(bp.id)) return false;
                }
            }

            if (entry.unlockKeys != null)
            {
                foreach (var key in entry.unlockKeys)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (!ledger.IsUnlocked(key)) return false;
                }
            }

            return true;
        }

        /// <summary>지금 이 항목을 걸 수 있는가. 걸 수 없으면 그 이유.</summary>
        public static ResearchReadiness Evaluate(ResearchEntrySO entry, Inventory inventory,
                                                 UnlockLedger ledger, ResearchQueue queue,
                                                 ItemDataSO energy)
        {
            if (entry == null || inventory == null) return ResearchReadiness.Invalid;
            if (!HasAnyUnlock(entry)) return ResearchReadiness.Invalid;
            if (entry.energyCost > 0 && energy == null) return ResearchReadiness.Invalid;

            if (IsKnown(entry, ledger)) return ResearchReadiness.AlreadyKnown;
            if (queue != null && queue.Contains(entry)) return ResearchReadiness.Queued;
            if (queue != null && queue.IsFull) return ResearchReadiness.QueueFull;

            if (entry.materials != null)
            {
                foreach (var need in entry.materials)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    if (!inventory.Has(need.item.id, need.count))
                        return ResearchReadiness.MissingMaterial;
                }
            }

            if (entry.energyCost > 0 &&
                !inventory.Has(EnergyIdOf(energy), entry.energyCost))
                return ResearchReadiness.MissingEnergy;

            return ResearchReadiness.Ready;
        }

        /// <summary>거절 사유를 그대로 화면에 띄울 한 줄.</summary>
        public static string Describe(ResearchReadiness readiness)
        {
            switch (readiness)
            {
                case ResearchReadiness.Ready:           return "분석할 수 있다";
                case ResearchReadiness.AlreadyKnown:    return "이미 밝혀냈다";
                case ResearchReadiness.Queued:          return "분석 중";
                case ResearchReadiness.QueueFull:       return "연구대가 가득 찼다";
                case ResearchReadiness.MissingMaterial: return "분석할 물건이 없다";
                case ResearchReadiness.MissingEnergy:   return "태울 스크랩이 모자란다";
                default:                                return "분석할 수 없다";
            }
        }

        /// <summary>
        /// 소재와 스크랩을 지금 빼고 줄 끝에 건다.
        ///
        /// 실패하면 아무것도 건드리지 않는다 — 반만 빠진 재료가 남으면 사용자는
        /// 자기가 무엇을 잃었는지 알 수 없다.
        /// </summary>
        public static bool TryEnqueue(ResearchQueue queue, ResearchEntrySO entry,
                                      Inventory source, UnlockLedger ledger, ItemDataSO energy)
        {
            if (queue == null) return false;
            if (Evaluate(entry, source, ledger, queue, energy) != ResearchReadiness.Ready) return false;

            if (entry.materials != null)
            {
                foreach (var need in entry.materials)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    source.TryRemove(need.item.id, need.count);
                }
            }

            if (entry.energyCost > 0) source.TryRemove(EnergyIdOf(energy), entry.energyCost);

            queue.Add(new ResearchJob(entry, entry.researchSeconds));
            queue.RaiseChanged();
            return true;
        }

        /// <summary>
        /// 시간을 흘린다. 맨 앞 항목만 진행한다.
        ///
        /// 다 본 항목은 그 자리에서 원장에 적힌다 — 산출물을 놓아 둘 회수함이 없으므로
        /// 완성과 수령이 한 순간이다.
        /// </summary>
        /// <returns>이번에 끝난 항목. 없으면 null.</returns>
        public static ResearchEntrySO Tick(ResearchQueue queue, float deltaSeconds,
                                           UnlockLedger ledger, bool powered = true)
        {
            if (queue == null || !powered || deltaSeconds <= 0f) return null;

            var job = queue.Active;
            if (job == null) return null;

            job.Elapsed += deltaSeconds;
            if (!job.IsDone) return null;

            queue.RemoveAt(0);
            Apply(job.Entry, ledger);
            queue.RaiseChanged();
            return job.Entry;
        }

        /// <summary>항목이 여는 것을 원장에 적는다.</summary>
        public static void Apply(ResearchEntrySO entry, UnlockLedger ledger)
        {
            if (entry == null || ledger == null) return;

            if (entry.unlocks != null)
            {
                foreach (var bp in entry.unlocks)
                {
                    if (bp == null) continue;
                    ledger.Unlock(bp.id);
                }
            }

            if (entry.unlockKeys != null)
            {
                foreach (var key in entry.unlockKeys)
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    ledger.Unlock(key);
                }
            }
        }

        /// <summary>
        /// 한 항목을 물린다. <b>끝나지 않은 것은 전부 돌려준다</b> — 반쯤 본 것의
        /// 스크랩만 태우는 규칙도 생각할 수 있지만, 제작대의 취소와 규칙이 달라지는
        /// 순간 사용자는 어느 쪽이 어떤지 외워야 한다.
        /// </summary>
        public static bool TryCancel(ResearchQueue queue, int index, Inventory refundTo,
                                     ItemDataSO energy)
        {
            if (queue == null) return false;
            var job = queue.At(index);
            if (job == null) return false;

            Refund(job.Entry, refundTo, energy);
            queue.RemoveAt(index);
            queue.RaiseChanged();
            return true;
        }

        /// <summary>줄 전체를 물린다. 돌려준 항목 수.</summary>
        public static int CancelAll(ResearchQueue queue, Inventory refundTo, ItemDataSO energy)
        {
            if (queue == null || queue.IsEmpty) return 0;

            int n = queue.Count;
            for (int i = 0; i < n; i++) Refund(queue.At(i)?.Entry, refundTo, energy);
            queue.Clear();
            queue.RaiseChanged();
            return n;
        }

        /// <summary>줄 전체가 끝날 때까지 남은 시간(초).</summary>
        public static float TotalSecondsLeft(ResearchQueue queue)
        {
            if (queue == null) return 0f;

            float sum = 0f;
            foreach (var job in queue.Jobs) sum += job.SecondsLeft;
            return sum;
        }

        static void Refund(ResearchEntrySO entry, Inventory refundTo, ItemDataSO energy)
        {
            if (entry == null || refundTo == null) return;

            if (entry.materials != null)
            {
                foreach (var need in entry.materials)
                {
                    if (need?.item == null || need.count <= 0) continue;
                    refundTo.TryAdd(need.item, need.count);
                }
            }

            if (entry.energyCost > 0 && energy != null) refundTo.TryAdd(energy, entry.energyCost);
        }
    }
}
