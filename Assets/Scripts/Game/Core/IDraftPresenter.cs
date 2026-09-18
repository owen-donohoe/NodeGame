using System.Collections.Generic;
using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.Core
{
    /// <summary>
    /// Whatever draws the draft. The same shape as ICountdownPresenter, and for
    /// the same reason: two UI stacks are live at once, DraftManager owns the
    /// turn machine and must not care which one is drawing.
    ///
    /// The methods are the ones DraftManager already called on the concrete
    /// uGUI DraftUI, lifted verbatim so that class satisfies this interface
    /// without a line changing inside it. Initialize and GetPersistentPlacements
    /// are here too because GameManager calls them, and it has the same choice
    /// to make.
    ///
    /// THE PRESENTER IS ASKED, NOT TOLD, ABOUT THE PARKED PIECE.
    /// TryGetPendingPlacement is the one call that goes the other way. A piece
    /// sitting on a cell waiting for Confirm is already the player's answer to
    /// "where", and the timeout takes it rather than throwing it away for a
    /// random cell. That decision lives in DraftManager.HandleTimeout; the
    /// presenter only reports what is on screen.
    /// </summary>
    public interface IDraftPresenter
    {
        /// <summary>Called by GameManager once, before the draft starts.</summary>
        void Initialize(DraftManager manager, int localPlayerID);

        /// <summary>The fixed placements (cores) drop in before turns begin.</summary>
        void ShowInitialReveal(BoardConfigData.InitialNodePlacement[] placements);

        /// <summary>Active drafting has begun: bring the surface on screen.</summary>
        void SweepIn(DraftState state, int localPlayerID);

        /// <summary>The draft is over: take the surface off screen.</summary>
        void SweepOut();

        /// <summary>Called every frame of ActiveDraft with the turn clock.</summary>
        void UpdateTimer(float remaining, float total);

        /// <summary>The turn passed to the other player.</summary>
        void OnTurnChanged(DraftState state, int localPlayerID);

        /// <summary>A placement was applied, by either player, for any reason.</summary>
        void OnPlacementConfirmed(DraftPlacement placement);

        /// <summary>
        /// The world-space objects standing on the board when the draft ends.
        /// MatchTransitionController takes them over and dissolves them as the
        /// real nodes arrive, so they must outlive the presenter.
        /// </summary>
        List<GameObject> GetPersistentPlacements();

        /// <summary>
        /// The placement the player has parked but not confirmed, if any.
        /// False when nothing is parked - mid-drag does not count, because the
        /// finger is still moving and nothing has been chosen yet.
        /// </summary>
        bool TryGetPendingPlacement(out int slotIndex, out int gridX, out int gridZ);
    }
}
