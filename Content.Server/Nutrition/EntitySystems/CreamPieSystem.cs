using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Nutrition.Components;
using Content.Server.Popups;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Explosion.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Rejuvenate;
using Content.Shared.Throwing;
using Content.Shared.Chemistry.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.Nutrition.EntitySystems
{
    [UsedImplicitly]
    public sealed class CreamPieSystem : SharedCreamPieSystem
    {
        [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
        [Dependency] private readonly PuddleSystem _puddle = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
        [Dependency] private readonly TriggerSystem _trigger = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly PopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            // activate BEFORE entity is deleted and trash is spawned
            SubscribeLocalEvent<CreamPieComponent, ConsumeDoAfterEvent>(OnConsume, before: [typeof(FoodSystem)]);
            SubscribeLocalEvent<CreamPieComponent, SliceFoodEvent>(OnSlice);

            SubscribeLocalEvent<CreamPiedComponent, RejuvenateEvent>(OnRejuvenate);
        }

        protected override void SplattedCreamPie(EntityUid uid, CreamPieComponent creamPie)
        {
            // The entity is deleted, so play the sound at its position rather than parenting
            var coordinates = Transform(uid).Coordinates;
            _audio.PlayPvs(_audio.ResolveSound(creamPie.Sound), coordinates, AudioParams.Default.WithVariation(0.125f));

            if (TryComp(uid, out FoodComponent? foodComp))
            {
                if (_solutions.TryGetSolution(uid, foodComp.Solution, out _, out var solution))
                {
                    _puddle.TrySpillAt(uid, solution, out _, false);
                }
                foreach (var trash in foodComp.Trash)
                {
                    Spawn(trash, Transform(uid).Coordinates);
                }
            }
            ActivatePayload(uid);

            QueueDel(uid);
        }

        private void OnConsume(Entity<CreamPieComponent> entity, ref ConsumeDoAfterEvent args)
        {
            ActivatePayload(entity);
        }

        private void OnSlice(Entity<CreamPieComponent> entity, ref SliceFoodEvent args)
        {
            ActivatePayload(entity);
        }

        private void ActivatePayload(EntityUid uid)
        {
            if (_itemSlots.TryGetSlot(uid, CreamPieComponent.PayloadSlotName, out var itemSlot))
            {
                if (_itemSlots.TryEject(uid, itemSlot, user: null, out var item))
                {
                    if (TryComp<OnUseTimerTriggerComponent>(item.Value, out var timerTrigger))
                    {
                        _trigger.HandleTimerTrigger(
                            item.Value,
                            null,
                            timerTrigger.Delay,
                            timerTrigger.BeepInterval,
                            timerTrigger.InitialBeepDelay,
                            timerTrigger.BeepSound);
                    }
                }
            }
        }

        protected override void CreamedEntity(EntityUid uid, CreamPiedComponent creamPied, ThrowHitByEvent args)
        {
            _popup.PopupEntity(Loc.GetString("cream-pied-component-on-hit-by-message",
                                            ("thrown", Identity.Entity(args.Thrown, EntityManager))),
                                            uid, args.Target);

            var otherPlayers = Filter.PvsExcept(uid);

            _popup.PopupEntity(Loc.GetString("cream-pied-component-on-hit-by-message-others",
                                            ("owner", Identity.Entity(uid, EntityManager)),
                                            ("thrown", Identity.Entity(args.Thrown, EntityManager))),
                                            uid, otherPlayers, false);
        }

        private void OnRejuvenate(Entity<CreamPiedComponent> entity, ref RejuvenateEvent args)
        {
            SetCreamPied(entity, entity.Comp, false);
        }
    }
}
