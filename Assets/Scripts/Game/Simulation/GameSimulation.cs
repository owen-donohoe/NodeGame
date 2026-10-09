using System.Collections.Generic;

namespace NodeWar.Simulation
{
    public static class GameSimulation
    {
        private static GameBalanceData bal;

        public static void SetBalance(GameBalanceData balance)
        {
            bal = balance;
        }

        // ===== MAIN TICK =====

        /// <summary>
        /// Advances the simulation by one tick. Called at fixed rate (10hz).
        /// Tick order:
        /// 1. Commands (handled by TickRunner before this call)
        /// 2. Movement (with combat interruption and breach-on-arrival)
        /// 3. Combat (detect fights, process cooldowns, deal damage, handle deaths)
        /// 4. Breach channel, structure damage, then claim bars
        /// 5. Production
        /// 6. Healing (normal or owned-Infirmary interval)
        /// 7. Respawn timers
        /// 8. Win condition (new breach at threshold; simultaneous losses cancel)
        /// 9. Post-combat resume (fight ended, determine next state)
        ///
        /// log, when given, receives what the tick did (see TickEventLog). It is
        /// only ever appended to, so passing one or not cannot change the result.
        /// </summary>
        public static void SimulateTick(SimulationState state, TickEventLog log = null)
        {
            state.tickCount++;
            int[] ownersAtTickStart = new int[state.nodes.Length];
            for (int i = 0; i < state.nodes.Length; i++) ownersAtTickStart[i] = state.nodes[i].ownerID;
            BuildResistanceSnapshot(state, ownersAtTickStart, out int[] resistancePercent, out _);
            TickTempoEvents(state.tickCount, log);

            // Step 2: Movement
            TickAllMovement(state, log);


            // Step 3: Combat
            TickCombat(state, log);

            // Step 4: Claiming
            bool[] breachedThisTick = TickBreach(state, ownersAtTickStart, resistancePercent, log);
            bool[] structureParticipants = TickStructureAttacks(state);
            TickClaiming(state, ownersAtTickStart, resistancePercent, structureParticipants, log);

            // Step 5: Production
            TickProduction(state);
            TickMinionProduction(state);
            TickBankCollection(state);
            TickAutoRecruit(state);

            // Step 6: Healing
            TickHealing(state);

            // Step 7: Respawns
            TickRespawns(state, log);

            // Step 8: Win condition
            TickWinCondition(state, breachedThisTick);

            // Step 9: Post-combat resume
            // Separated from step 3 to avoid state thrashing within a single tick.
            // Combat resolves, deaths happen, THEN survivors figure out what to do next.
            // This prevents a villager from killing an enemy and immediately starting to
            // claim in the same tick, which could cause edge cases with the claim
            // evaluation also running in step 4.
            TickOrderResume(state, structureParticipants, log);
            // Derived cache, after every rule mutation (including respawns/resume).
            for (int p = 0; p < state.players.Length; p++)
            {
                int candidate = bal.BreachBarEnabled() ? FindBreacher(state, p, out _) : -1;
                state.players[p].nextBreacherID = candidate < 0 ? -1 : state.villagers[candidate].villagerID;
            }
        }

        // ===== STEP 2: MOVEMENT =====

        private static void TickAllMovement(SimulationState state, TickEventLog log)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].state == VillagerState.Moving)
                {
                    TickMovement(state, i, log);
                }
            }
        }

        private static void TickMovement(SimulationState state, int villagerIndex, TickEventLog log)
        {
            VillagerData v = state.villagers[villagerIndex];

            // Invariant: a Moving villager always has a further node ahead on its path.
            // If that is violated the villager is in a corrupt movement state -- recover
            // deterministically rather than indexing off the end of movePath below.
            if (v.movePath == null || v.movePathIndex + 1 >= v.movePath.Length)
            {
                v.movePath = new int[0];
                v.movePathIndex = 0;
                v.moveProgress = 0;
                v.state = VillagerState.Idle;
                state.villagers[villagerIndex] = v;
                return;
            }

            v.moveProgress++;

            // Calculate ticks needed for current edge
            int edgeWeight = GetLinkWeight(state, v.movePath[v.movePathIndex], v.movePath[v.movePathIndex + 1]);
            int ticksForEdge = edgeWeight * v.moveSpeedTicks;

            if (v.moveProgress >= ticksForEdge)
            {
                // Advance to next node in path
                v.previousNodeID = v.movePath[v.movePathIndex];
                v.movePathIndex++;
                v.moveProgress = 0;

                v.currentNodeID = v.movePath[v.movePathIndex];

                // --- Check for combat interruption or breach on EVERY node arrival ---
                int enemyCoreID = state.players[1 - v.ownerID].coreNodeID;

                bool enemiesPresent = HasLivingEnemiesOnNode(state, v.currentNodeID, v.ownerID);

                if (v.currentNodeID == enemyCoreID)
                {
                    if (enemiesPresent)
                    {
                        v.state = VillagerState.Fighting;
                        v.moveProgress = 0;
                        v.attackCooldownRemaining = v.attackCooldownMax;
                        v.combatTargetID = -1;
                        state.villagers[villagerIndex] = v;
                        return;
                    }
                    else
                    {
                        EnterBreachOrProcessLegacy(state, villagerIndex, v, log);
                        return;
                    }
                }

                if (enemiesPresent)
                {
                    v.state = VillagerState.Fighting;
                    v.moveProgress = 0;
                    v.attackCooldownRemaining = v.attackCooldownMax;
                    v.combatTargetID = -1;
                    state.villagers[villagerIndex] = v;
                    return;
                }

                // --- Normal arrival logic (no enemies) ---
                if (v.movePathIndex >= v.movePath.Length - 1)
                {
                    v.movePath = new int[0];
                    v.movePathIndex = 0;
                    v.moveProgress = 0;
                    v.state = VillagerState.Idle;
                    state.villagers[villagerIndex] = v;
                    if (v.targetNodeID == v.currentNodeID || v.targetNodeID < 0)
                    {
                        state.villagers[villagerIndex].targetNodeID = -1;
                        ApplyArrivalState(state, villagerIndex);
                    }
                    return;
                }
            }

            state.villagers[villagerIndex] = v;
        }

        /// <summary>
        /// Drops a half-walked reversal leg, leaving the villager standing on
        /// currentNodeID with the rest of its route intact.
        ///
        /// A reversing villager is between currentNodeID and movePath[movePathIndex],
        /// walking back -- the one case where those two disagree. Anything that
        /// zeroes moveProgress, combat above all, would otherwise place it at the
        /// far end of that leg: a node it turned around before ever reaching, and
        /// which it would then walk the whole leg back from.
        ///
        /// Cancelling the return instead is both correct and what a player expects.
        /// A fight breaking out where you are standing ends the retreat you were
        /// in the middle of; it does not teleport you to where you were headed.
        /// </summary>
        private static void CollapseReversalLeg(SimulationState state, int villagerIndex)
        {
            VillagerData v = state.villagers[villagerIndex];

            if (v.movePath == null) return;
            if (v.movePathIndex >= v.movePath.Length) return;
            if (v.movePath[v.movePathIndex] == v.currentNodeID) return;

            // Everything from currentNodeID onward. It sits one past the
            // abandoned node, which is what movePathIndex still points at.
            int remaining = v.movePath.Length - v.movePathIndex - 1;

            if (remaining < 1)
            {
                state.villagers[villagerIndex].movePath = new int[0];
                state.villagers[villagerIndex].movePathIndex = 0;
                return;
            }

            int[] collapsed = new int[remaining];
            for (int i = 0; i < remaining; i++)
                collapsed[i] = v.movePath[v.movePathIndex + 1 + i];

            state.villagers[villagerIndex].movePath = collapsed;
            state.villagers[villagerIndex].movePathIndex = 0;
        }

        /// <summary>
        /// Applies suit assignment, production timer, and state for a villager
        /// that has arrived at a node (end of path or post-combat with no path).
        /// Directly modifies state.villagers[villagerIndex].
        /// </summary>
        internal static void ApplyArrivalState(SimulationState state, int villagerIndex)
        {
            VillagerData v = state.villagers[villagerIndex];
            int nodeID = v.currentNodeID;
            NodeData node = state.nodes[nodeID];

            if (StructureRules.IsSelectedAttacker(state, villagerIndex, bal))
            {
                state.villagers[villagerIndex].state = VillagerState.AttackingStructure;
                return;
            }

            // Core nodes: always Idle.
            // Non-combat suits (Farmer, Miner, Smelter) are free and re-assigned on arrival
            // at production nodes, so reverting them here is harmless and keeps things clean.
            // Soldier suit is PERMANENT until death â€” do not strip it.
            if (node.districtType == DistrictType.Core)
            {
                if (!GameBalanceData.IsCombatSuit(v.suit))
                {
                    state.villagers[villagerIndex].suit = SuitType.None;
                    state.villagers[villagerIndex].attackDamage = bal.baseAttackDamage;
                    state.villagers[villagerIndex].moveSpeedTicks = bal.baseMoveSpeedTicks;
                    state.villagers[villagerIndex].attackCooldownMax = bal.baseAttackCooldownMax;
                }
                state.villagers[villagerIndex].state = VillagerState.Idle;
                return;
            }

            // Own node
            if (node.ownerID == v.ownerID)
            {
                if (GameBalanceData.IsCombatSuit(v.suit))
                {
                    state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                SuitType expectedSuit = GetExpectedSuit(node.districtType);
                if (expectedSuit != SuitType.None)
                {
                    state.villagers[villagerIndex].suit = expectedSuit;
                    if (node.districtType == DistrictType.Infirmary ? InfirmaryWorkerSlot(state, villagerIndex) : BankRules.HasWorkerSlot(state, villagerIndex, bal))
                    {
                        int ticks = GetProductionTicks(node.districtType, node.districtEra);
                        state.villagers[villagerIndex].productionTicksMax = ticks;
                        state.villagers[villagerIndex].productionTicksRemaining = ticks;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                // Barracks, Rampart, Village, None â€” strip non-combat suit, go Idle
                if (!GameBalanceData.IsCombatSuit(v.suit))
                {
                    state.villagers[villagerIndex].suit = SuitType.None;
                    state.villagers[villagerIndex].attackDamage = bal.baseAttackDamage;
                    state.villagers[villagerIndex].moveSpeedTicks = bal.baseMoveSpeedTicks;
                    state.villagers[villagerIndex].attackCooldownMax = bal.baseAttackCooldownMax;
                }
                state.villagers[villagerIndex].state = VillagerState.Idle;
                return;
            }

            // Not own node: Claiming logic
            int friendlyClaimers = CountFriendlyClaimersOnNode(state, nodeID, v.ownerID);
            if (friendlyClaimers < bal.maxClaimersPerNode)
                state.villagers[villagerIndex].state = VillagerState.Claiming;
            else
                state.villagers[villagerIndex].state = VillagerState.Idle;
        }

        // ===== STEP 3: COMBAT =====

        private static void TickCombat(SimulationState state, TickEventLog log)
        {
            // Phase A: Build player presence once, then interrupt villagers on contested nodes.
            int[] presence = new int[state.nodes.Length];
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.state == VillagerState.Dead || vil.isConsumed) continue;
                presence[vil.currentNodeID] |= vil.ownerID == 0 ? 1 : 2;
            }

            // A fight starts on a node when someone there is first pulled into
            // it here. An arrival that walks into enemies is already Fighting by
            // now, but whoever it walked into is not, so every new fight passes
            // through this loop once; a villager joining a fight already under
            // way finds nobody left to pull in and starts nothing. Only built
            // when recording, so the unrecorded tick allocates what it always did.
            bool[] combatAnnounced = log != null ? new bool[state.nodes.Length] : null;

            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.state == VillagerState.Dead || vil.isConsumed) continue;
                if (presence[vil.currentNodeID] != 3) continue;
                if (vil.state == VillagerState.Fighting) continue;

                if (combatAnnounced != null && !combatAnnounced[vil.currentNodeID])
                {
                    combatAnnounced[vil.currentNodeID] = true;
                    log.Add(TickEventType.CombatStarted, vil.currentNodeID, -1, -1, 0);
                }

                state.villagers[v].state = VillagerState.Fighting;
                state.villagers[v].attackCooldownRemaining = vil.attackCooldownMax;
                state.villagers[v].combatTargetID = -1;
                state.villagers[v].moveProgress = 0;

                // Zero progress leaves the villager on its current node, so discard
                // any half-walked reversal leg that points at a different node.
                CollapseReversalLeg(state, v);
            }

            // Phase B: Assign round-robin targets for all contested nodes
            AssignAllCombatTargets(state);

            // Phase C: Process attack cooldowns and deal damage
            for (int v = 0; v < state.villagers.Length; v++)
            {
                if (state.villagers[v].state != VillagerState.Fighting) continue;
                if (state.villagers[v].isConsumed) continue;

                state.villagers[v].attackCooldownRemaining--;

                if (state.villagers[v].attackCooldownRemaining <= 0)
                {
                    if (state.villagers[v].suit == SuitType.Medic)
                    {
                        int healTarget = FindMostDamagedFriendly(state,
                            state.villagers[v].currentNodeID, state.villagers[v].ownerID, v);
                        if (healTarget >= 0)
                        {
                            state.villagers[healTarget].hp++;
                            if (state.villagers[healTarget].hp > state.villagers[healTarget].maxHP)
                                state.villagers[healTarget].hp = state.villagers[healTarget].maxHP;
                        }
                    }
                    else
                    {
                        int targetID = state.villagers[v].combatTargetID;
                        if (targetID >= 0 && targetID < state.villagers.Length)
                        {
                            if (state.villagers[targetID].state != VillagerState.Dead &&
                                !state.villagers[targetID].isConsumed)
                            {
                                int damage = state.villagers[v].attackDamage;
                                state.villagers[targetID].hp -= damage;
                            }
                        }
                    }
                    state.villagers[v].attackCooldownRemaining = state.villagers[v].attackCooldownMax;
                }
            }

            // Phase D: Handle deaths
            for (int v = 0; v < state.villagers.Length; v++)
            {
                if (state.villagers[v].state == VillagerState.Dead) continue;
                if (state.villagers[v].isConsumed) continue;

                if (state.villagers[v].hp <= 0)
                {
                    log?.Add(TickEventType.VillagerDied, state.villagers[v].currentNodeID, v,
                             state.villagers[v].ownerID, 0);

                    state.villagers[v].state = VillagerState.Dead;
                    state.villagers[v].hp = 0;
                    state.villagers[v].respawnTicksRemaining = bal.respawnTicks;
                    state.villagers[v].movePath = new int[0];
                    state.villagers[v].movePathIndex = 0;
                    state.villagers[v].moveProgress = 0;
                    state.villagers[v].targetNodeID = -1;
                    state.villagers[v].combatTargetID = -1;
                }
            }
        }

        /// <summary>
        /// Assigns round-robin combat targets for all nodes with active combat.
        /// Called after setting Fighting states and after deaths.
        /// </summary>
        private static void AssignAllCombatTargets(SimulationState state)
        {
            // Array-backed per-node chains preserve ascending villager ID in one pass.
            int[] first = new int[state.nodes.Length];
            int[] last = new int[state.nodes.Length];
            int[] next = new int[state.villagers.Length];
            for (int n = 0; n < first.Length; n++)
                first[n] = last[n] = -1;
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.state != VillagerState.Fighting || vil.isConsumed) continue;
                int node = vil.currentNodeID;
                next[v] = -1;
                if (first[node] < 0) first[node] = v;
                else next[last[node]] = v;
                last[node] = v;
            }

            List<int> p0Fighters = new List<int>();
            List<int> p1Fighters = new List<int>();
            List<int> p0Targets = new List<int>();
            List<int> p1Targets = new List<int>();
            System.Comparison<int> compareTargets = (a, b) =>
            {
                int priority = state.villagers[b].fightPriority.CompareTo(state.villagers[a].fightPriority);
                return priority != 0 ? priority : a.CompareTo(b);
            };

            for (int nodeIndex = 0; nodeIndex < state.nodes.Length; nodeIndex++)
            {
                p0Fighters.Clear();
                p1Fighters.Clear();
                p0Targets.Clear();
                p1Targets.Clear();
                for (int v = first[nodeIndex]; v >= 0; v = next[v])
                {
                    if (state.villagers[v].ownerID == 0) p0Fighters.Add(v);
                    else p1Fighters.Add(v);
                }

                if (p0Fighters.Count == 0 || p1Fighters.Count == 0) continue;

                // Only targets are priority-sorted; attackers retain ascending ID order.
                p0Targets.AddRange(p0Fighters);
                p1Targets.AddRange(p1Fighters);
                p0Targets.Sort(compareTargets);
                p1Targets.Sort(compareTargets);

                // Assign round-robin: P0 attackers -> P1 targets
                for (int i = 0; i < p0Fighters.Count; i++)
                {
                    int targetIndex = i % p1Targets.Count;
                    state.villagers[p0Fighters[i]].combatTargetID = p1Targets[targetIndex];
                }

                // Assign round-robin: P1 attackers -> P0 targets
                for (int i = 0; i < p1Fighters.Count; i++)
                {
                    int targetIndex = i % p0Targets.Count;
                    state.villagers[p1Fighters[i]].combatTargetID = p0Targets[targetIndex];
                }
            }
        }

        // ===== STEP 4: CLAIMING =====

        private static bool[] TickStructureAttacks(SimulationState state)
        {
            bool[] participants = new bool[state.villagers.Length];
            for (int nodeID = 0; nodeID < state.nodes.Length; nodeID++)
            {
                int attackers = 0;
                // Select everyone before applying damage, even when HP is only 1.
                for (int i = 0; i < state.villagers.Length; i++)
                {
                    VillagerData v = state.villagers[i];
                    if (v.currentNodeID != nodeID || v.state == VillagerState.Fighting ||
                        !StructureRules.IsSelectedAttacker(state, i, bal)) continue;
                    participants[i] = true;
                    state.villagers[i].state = VillagerState.AttackingStructure;
                    attackers++;
                }
                if (attackers == 0) continue;
                NodeData node = state.nodes[nodeID];
                node.structureHP = (int)System.Math.Max(0, (long)node.structureHP - (long)attackers * bal.structureDamagePerTick);
                if (node.structureHP == 0)
                {
                    if (node.structureKind == StructureKind.Minion)
                        BankRules.PayBank(state, node, 1 - node.ownerID, bal);
                    node = StructureRules.Destroy(node);
                }
                state.nodes[nodeID] = node;
            }
            return participants;
        }

        private static void TickClaiming(SimulationState state, int[] ownersAtTickStart, int[] resistancePercent, bool[] structureParticipants, TickEventLog log)
        {
            // Re-evaluate Idle/Claiming states based on current ownership
            UpdateVillagerClaimStates(state, structureParticipants);

            // Process claim bars per node
            for (int nodeIndex = 0; nodeIndex < state.nodes.Length; nodeIndex++)
            {
                NodeData node = state.nodes[nodeIndex];

                if (node.districtType == DistrictType.Core) continue;
                TryRestoreOwnedNode(state, nodeIndex);
                node = state.nodes[nodeIndex];

                int p0Claimers = 0;
                int p1Claimers = 0;

                for (int v = 0; v < state.villagers.Length; v++)
                {
                    VillagerData vil = state.villagers[v];
                    if (vil.currentNodeID != nodeIndex) continue;
                    if (vil.state != VillagerState.Claiming) continue;

                    if (vil.ownerID == 0) p0Claimers++;
                    else p1Claimers++;
                }

                // Contested: both present -> frozen (combat handles this via Fighting state,
                // but kept as safety check)
                if (p0Claimers > 0 && p1Claimers > 0) continue;

                if (p0Claimers == 0 && p1Claimers == 0) continue;

                // --- Player 0 claiming ---
                if (p0Claimers > 0 && node.ownerID != 0)
                {
                    long rate = ClaimRate(state, nodeIndex, 0, p0Claimers, ownersAtTickStart, resistancePercent);
                    node.claimBar = (int)System.Math.Min(bal.claimThreshold, (long)node.claimBar + rate);

                    if (node.claimBar >= bal.claimThreshold)
                    {
                        node.claimBar = bal.claimThreshold;
                        state.nodes[nodeIndex] = node;
                        CompleteClaimForPlayer(state, nodeIndex, 0, log);
                        node = state.nodes[nodeIndex];
                    }
                    else if (node.ownerID == 1 && node.claimBar >= 0)
                    {
                        node.claimBar = 0;
                        node = OnOwnershipChanged(state, node, node.ownerID, -1, log);
                    }
                }

                // --- Player 1 claiming ---
                if (p1Claimers > 0 && node.ownerID != 1)
                {
                    long rate = ClaimRate(state, nodeIndex, 1, p1Claimers, ownersAtTickStart, resistancePercent);
                    node.claimBar = (int)System.Math.Max(-(long)bal.claimThreshold, (long)node.claimBar - rate);

                    if (node.claimBar <= -bal.claimThreshold)
                    {
                        node.claimBar = -bal.claimThreshold;
                        state.nodes[nodeIndex] = node;
                        CompleteClaimForPlayer(state, nodeIndex, 1, log);
                        node = state.nodes[nodeIndex];
                    }
                    else if (node.ownerID == 0 && node.claimBar <= 0)
                    {
                        node.claimBar = 0;
                        node = OnOwnershipChanged(state, node, node.ownerID, -1, log);
                    }
                }

                state.nodes[nodeIndex] = node;
            }

            UpdateVillagerClaimStates(state, structureParticipants);
        }

        // ===== STEP 5: PRODUCTION =====

        public static long FrontierPercent(SimulationState state, int nodeID, int attackerID, int[] ownersAtTickStart)
        {
            int net = 0;
            Link[] links = state.nodes[nodeID].links;
            for (int i = 0; i < links.Length; i++)
            {
                int owner = ownersAtTickStart[links[i].toNodeID];
                if (owner == attackerID) net++;
                else if (owner == 1 - attackerID) net--;
            }
            int steps = System.Math.Max(0, System.Math.Min(bal.captureBonusMaxSteps, net));
            return 100L + (long)bal.captureBonusPercentPerStep * steps;
        }

        /// <summary>Tick-local aura, read solely from the ownership snapshot and paid levels.</summary>
        public static void BuildResistanceSnapshot(SimulationState state, int[] ownersAtTickStart,
            out int[] resistancePercent, out int[] resistanceSourceNodeID)
        {
            resistancePercent = new int[state.nodes.Length];
            resistanceSourceNodeID = new int[state.nodes.Length];
            for (int i = 0; i < state.nodes.Length; i++) resistanceSourceNodeID[i] = -1;
            for (int source = 0; source < state.nodes.Length; source++)
            {
                NodeData node = state.nodes[source];
                int owner = ownersAtTickStart[source];
                if (owner < 0 || owner > 1 || node.districtType != DistrictType.Fortress || node.fortressLevel < 1 || node.fortressLevel > 3) continue;
                DistrictStats stats = bal.GetDistrictStats(DistrictType.Fortress, node.districtEra);
                if (!GameBalanceData.FortressStatsValid(stats)) continue;
                int percent = stats.fortressResistancePercent[node.fortressLevel];
                ApplyResistance(source, source, owner, percent, ownersAtTickStart, resistancePercent, resistanceSourceNodeID);
                if (node.links != null)
                    for (int edge = 0; edge < node.links.Length; edge++)
                        ApplyResistance(node.links[edge].toNodeID, source, owner, percent, ownersAtTickStart, resistancePercent, resistanceSourceNodeID);
            }
        }

        private static void ApplyResistance(int target, int source, int owner, int percent, int[] owners,
            int[] resistancePercent, int[] resistanceSourceNodeID)
        {
            if (target < 0 || target >= owners.Length || owners[target] != owner || percent <= 0) return;
            if (percent > resistancePercent[target] || (percent == resistancePercent[target] &&
                (resistanceSourceNodeID[target] < 0 || source < resistanceSourceNodeID[target])))
            {
                resistancePercent[target] = percent;
                resistanceSourceNodeID[target] = source;
            }
        }

        private static long ResistRate(long rate, int resistance)
        {
            long divisor = 100L + resistance;
            // Equivalent integer floor, without overflowing an already-long claim product.
            return System.Math.Max(1, rate / divisor * 100 + rate % divisor * 100 / divisor);
        }

        public static long ClaimRate(SimulationState state, int nodeID, int attackerID, int bodies, int[] ownersAtTickStart)
        {
            BuildResistanceSnapshot(state, ownersAtTickStart, out int[] resistancePercent, out _);
            return ClaimRate(state, nodeID, attackerID, bodies, ownersAtTickStart, resistancePercent);
        }

        public static long ClaimRate(SimulationState state, int nodeID, int attackerID, int bodies, int[] ownersAtTickStart, int[] resistancePercent)
        {
            if (bodies <= 0 || bal.baseClaimPerTick <= 0) return 0;
            long rate = (long)bal.baseClaimPerTick * System.Math.Min(4, bodies);
            int bar = state.nodes[nodeID].claimBar;
            if ((attackerID == 0 && bar < 0) || (attackerID == 1 && bar > 0))
                rate = checked(rate * bal.decrementMultiplier);
            rate = checked(rate * FrontierPercent(state, nodeID, attackerID, ownersAtTickStart)) / 100;
            rate = checked(rate * bal.TempoPercent(bal.tempoClaimPercent, state.tickCount)) / 100;
            if (ownersAtTickStart[nodeID] == 1 - attackerID)
                rate = ResistRate(rate, resistancePercent[nodeID]);
            // A single tick may cross the entire signed bar, which spans twice
            // int.MaxValue. Bound only beyond that observationally equivalent range.
            return System.Math.Min(2L * int.MaxValue, System.Math.Max(1, rate));
        }

        private static void TryRestoreOwnedNode(SimulationState state, int nodeID)
        {
            NodeData node = state.nodes[nodeID];
            if (node.districtType == DistrictType.Core || node.ownerID < 0 || node.ownerID > 1) return;
            int bodies = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.currentNodeID != nodeID || v.state == VillagerState.Dead || v.isConsumed || v.hp <= 0) continue;
                if (v.ownerID != node.ownerID) return;
                if (v.state == VillagerState.Working || v.state == VillagerState.Idle || v.state == VillagerState.Claiming)
                    bodies = System.Math.Min(4, bodies + 1);
            }
            long rate = checked((long)bal.baseClaimPerTick * bodies * bal.TempoPercent(bal.tempoClaimPercent, state.tickCount)) / 100;
            rate = System.Math.Min(2L * int.MaxValue, rate);
            if (node.ownerID == 0 && node.claimBar < bal.claimThreshold)
                node.claimBar = (int)System.Math.Min(bal.claimThreshold, (long)node.claimBar + rate);
            else if (node.ownerID == 1 && node.claimBar > -(long)bal.claimThreshold)
                node.claimBar = (int)System.Math.Max(-(long)bal.claimThreshold, (long)node.claimBar - rate);
            state.nodes[nodeID] = node;
        }

        private static void TickAutoRecruit(SimulationState state)
        {
            // MatchFactory assigns compact ascending node IDs in array order.
            for (int nodeID = 0; nodeID < state.nodes.Length; nodeID++)
                if (state.nodes[nodeID].autoRecruit)
                    CommandProcessor.TryRecruit(state, state.nodes[nodeID].ownerID, nodeID);
        }

        private static void TickBankCollection(SimulationState state)
        {
            for (int nodeID = 0; nodeID < state.nodes.Length; nodeID++)
                state.nodes[nodeID] = BankRules.TickCollection(state, state.nodes[nodeID], bal);
        }

        private static void TickMinionProduction(SimulationState state)
        {
            int decrement = bal.ProductionTempoValid()
                ? bal.TimerDecrement(bal.tempoProductionPercent, state.tickCount) : 1;
            for (int i = 0; i < state.nodes.Length; i++)
            {
                NodeData node = state.nodes[i];
                if (!BankRules.CanProduce(state, node, bal)) continue;
                node.minionProductionRemaining -= decrement;
                if (node.minionProductionRemaining <= 0)
                {
                    switch (node.districtType)
                    {
                        case DistrictType.Farm: node.bankFood++; break;
                        case DistrictType.Mine: node.bankMaterials++; break;
                        case DistrictType.Forge:
                            // Missing input/allocation wastes this cycle, as for human
                            // Forge workers. Only dormancy/full bank pauses the timer.
                            if (node.materialAllocation > 0 && state.players[node.ownerID].materials > 0)
                            { state.players[node.ownerID].materials--; node.bankMetal++; }
                            break;
                        case DistrictType.Storehouse:
                            if (node.storehouseNextResource == 0) node.bankFood++;
                            else node.bankMaterials++;
                            node.storehouseNextResource = 1 - node.storehouseNextResource;
                            break;
                    }
                    node.minionProductionRemaining += BankRules.ProductionTicks(node, bal);
                }
                state.nodes[i] = node;
            }
        }

        /// <summary>
        /// Every tick, decrements production timers for Working villagers.
        /// When a timer reaches 0: awards the appropriate resource to the owner
        /// and resets the timer for the next cycle.
        /// Farm -> +1 Food, Mine -> +1 Material, Forge -> +1 Metal (costs 1 Material, requires allocation > 0).
        /// </summary>
        private static void TickProduction(SimulationState state)
        {
            for (int idx = 0; idx < state.villagers.Length; idx++)
            {
                if (state.villagers[idx].state != VillagerState.Working) continue;
                if (state.villagers[idx].isConsumed) continue;
                if (state.villagers[idx].productionTicksMax <= 0) continue;

                int decrement = bal.ProductionTempoValid()
                    ? bal.TimerDecrement(bal.tempoProductionPercent, state.tickCount) : 1;
                state.villagers[idx].productionTicksRemaining -= decrement;

                if (state.villagers[idx].productionTicksRemaining <= 0)
                {
                    int ownerID = state.villagers[idx].ownerID;
                    int nodeID = state.villagers[idx].currentNodeID;
                    DistrictType district = state.nodes[nodeID].districtType;

                    switch (district)
                    {
                        case DistrictType.Farm:
                            state.players[ownerID].food = GameBalanceData.AddResource(state.players[ownerID].food, bal.foodCap);
                            break;

                        case DistrictType.Mine:
                            state.players[ownerID].materials = GameBalanceData.AddResource(state.players[ownerID].materials, bal.materialsCap);
                            break;

                        case DistrictType.Forge:
                            // Convert only with allocation, materials, and room for the metal.
                            if (state.nodes[nodeID].materialAllocation > 0 &&
                                state.players[ownerID].materials >= 1 &&
                                GameBalanceData.HasResourceRoom(state.players[ownerID].metal, bal.metalCap))
                            {
                                state.players[ownerID].materials--;
                                state.players[ownerID].metal = GameBalanceData.AddResource(state.players[ownerID].metal, bal.metalCap);
                            }
                            // A blocked conversion still cycles without consuming materials.
                            break;
                    }

                    // Reset timer for next production cycle
                    state.villagers[idx].productionTicksRemaining += state.villagers[idx].productionTicksMax;
                }
            }
        }

        private static void UpdateVillagerClaimStates(SimulationState state, bool[] structureParticipants)
        {
            for (int idx = 0; idx < state.villagers.Length; idx++)
            {
                VillagerData v = state.villagers[idx];

                if (idx < structureParticipants.Length && structureParticipants[idx])
                {
                    state.villagers[idx].state = StructureRules.IsSelectedAttacker(state, idx, bal)
                        ? VillagerState.AttackingStructure : VillagerState.Idle;
                    continue;
                }
                if (v.state == VillagerState.AttackingStructure)
                {
                    state.villagers[idx].state = VillagerState.Idle;
                    v = state.villagers[idx];
                }

                // Only re-evaluate Idle, Claiming, and Working villagers
                if (v.state != VillagerState.Idle && v.state != VillagerState.Claiming && v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;
                // Intent elsewhere forbids local work/claim, but leaves stationary
                // Idle presence available to passive rules.
                if (v.targetNodeID >= 0 && v.targetNodeID != v.currentNodeID)
                {
                    if (v.state != VillagerState.Idle)
                        state.villagers[idx].state = VillagerState.Idle;
                    continue;
                }

                NodeData node = state.nodes[v.currentNodeID];

                // Core nodes: always Idle
                if (node.districtType == DistrictType.Core)
                {
                    if (v.state != VillagerState.Idle)
                        state.villagers[idx].state = VillagerState.Idle;
                    continue;
                }

                // === OWN NODE ===
                if (node.ownerID == v.ownerID)
                {
                    // Soldiers never work, just Idle
                    if (GameBalanceData.IsCombatSuit(v.suit))
                    {
                        if (v.state != VillagerState.Idle)
                            state.villagers[idx].state = VillagerState.Idle;
                        continue;
                    }

                    // Is this a production node?
                    SuitType expectedSuit = GetExpectedSuit(node.districtType);

                    if (expectedSuit != SuitType.None)
                    {
                        // Production node: assign suit if needed
                        if (v.suit != expectedSuit)
                            state.villagers[idx].suit = expectedSuit;

                        // Try Working if not already
                        if (v.state != VillagerState.Working || node.districtType == DistrictType.Infirmary || !BankRules.HasWorkerSlot(state, idx, bal))
                        {
                            if (node.districtType == DistrictType.Infirmary ? InfirmaryWorkerSlot(state, idx) : BankRules.HasWorkerSlot(state, idx, bal))
                            {
                                state.villagers[idx].state = VillagerState.Working;
                                state.villagers[idx].productionTicksMax = GetProductionTicks(node.districtType, node.districtEra);
                                state.villagers[idx].productionTicksRemaining = GetProductionTicks(node.districtType, node.districtEra);
                            }
                            else
                            {
                                state.villagers[idx].state = VillagerState.Idle;
                            }
                        }
                        // If already Working, stay Working
                    }
                    else
                    {
                        // Non-production node (Barracks, Village, None): Idle
                        if (v.state != VillagerState.Idle)
                            state.villagers[idx].state = VillagerState.Idle;
                    }

                    continue;
                }

                // === NOT OWN NODE ===
                if (v.state != VillagerState.Claiming)
                {
                    int friendlyClaimers = CountFriendlyClaimersOnNode(state, v.currentNodeID, v.ownerID);
                    if (friendlyClaimers < bal.maxClaimersPerNode)
                    {
                        state.villagers[idx].state = VillagerState.Claiming;
                    }
                }
            }
        }

        private static NodeData OnOwnershipChanged(SimulationState state, NodeData node, int oldOwner, int newOwner, TickEventLog log)
        {
            if (oldOwner != newOwner)
            {
                node.autoRecruit = false;
                node.recruitReadyTick = 0;
                node.fortressLevel = 0;
                node.collectProgress = 0;
                node.collectRequested = false;
            }
            if (newOwner >= 0 && node.structureKind == StructureKind.Minion)
            {
                BankRules.PayBank(state, node, newOwner, bal);
                node = StructureRules.Destroy(node);
            }
            if (node.structureKind == StructureKind.Fortification) node = StructureRules.Destroy(node);
            node.ownerID = newOwner;
            if (newOwner < 0) log?.Add(TickEventType.NodeNeutralised, node.nodeID, -1, oldOwner, 1 - oldOwner);
            else log?.Add(TickEventType.NodeClaimed, node.nodeID, -1, newOwner, oldOwner);
            return node;
        }

        private static void CompleteClaimForPlayer(SimulationState state, int nodeIndex, int playerID, TickEventLog log)
        {
            state.nodes[nodeIndex] = OnOwnershipChanged(state, state.nodes[nodeIndex], state.nodes[nodeIndex].ownerID, playerID, log);

            if (state.nodes[nodeIndex].upgradeCategory != DistrictUpgradeCategory.Fixed)
            {
                DistrictType upgrade = GetPlayerUpgradeForSlot(state, playerID, state.nodes[nodeIndex].upgradeCategory);
                state.nodes[nodeIndex].districtType = upgrade != DistrictType.None
                    ? upgrade
                    : state.nodes[nodeIndex].baseDistrictType;

                // An upgrade plays the claimer's era of it. A slot reverting to
                // its base district plays era 0: slots are the board's, not a
                // player's, and no draft era is kept for them.
                state.nodes[nodeIndex].districtEra = upgrade != DistrictType.None
                    ? state.players[playerID].DistrictEra(upgrade)
                    : 0;

                // Reset non-combat workers â€” node type just changed
                for (int i = 0; i < state.villagers.Length; i++)
                {
                    if (state.villagers[i].currentNodeID != nodeIndex) continue;
                    if (state.villagers[i].state == VillagerState.Dead || state.villagers[i].isConsumed) continue;
                    if (GameBalanceData.IsCombatSuit(state.villagers[i].suit)) continue;
                    state.villagers[i].state = VillagerState.Idle;
                    state.villagers[i].suit = SuitType.None;
                    state.villagers[i].productionTicksRemaining = 0;
                    state.villagers[i].productionTicksMax = 0;
                }
            }

            NodeData claimed = state.nodes[nodeIndex];
            if (claimed.districtType == DistrictType.Storehouse && !claimed.storehouseInitialised &&
                bal.BankTuningValid() && BankRules.ProductionTicks(claimed, bal) > 0)
            {
                claimed.storehouseInitialised = true;
                state.nodes[nodeIndex] = BankRules.CreateMinion(claimed, bal);
            }

            int playerBit = 1 << playerID;
            if (state.nodes[nodeIndex].districtType == DistrictType.Town &&
                (state.nodes[nodeIndex].townPaidMask & playerBit) == 0)
            {
                // Consume the entitlement before the cap check; no deferred credit.
                state.nodes[nodeIndex].townPaidMask |= playerBit;
                int bonus = bal.GetDistrictStats(DistrictType.Town, state.nodes[nodeIndex].districtEra).townBonusVillagers;
                int room = System.Math.Max(0, bal.maxVillagersPerPlayer - NodeActionRules.CountPopulation(state, playerID));
                SpawnBonusVillagers(state, nodeIndex, playerID, System.Math.Min(bonus, room));
            }
        }

        private static DistrictType GetPlayerUpgradeForSlot(SimulationState state, int playerID, DistrictUpgradeCategory upgradeCategory)
        {
            int[] draftedDistricts = state.players[playerID].draftedDistricts;
            if (draftedDistricts == null) return DistrictType.None;
            for (int i = 0; i < draftedDistricts.Length; i++)
            {
                DistrictType drafted = (DistrictType)draftedDistricts[i];
                if (GameBalanceData.GetUpgradeCategoryForDistrict(drafted) == upgradeCategory)
                    return drafted;
            }
            return DistrictType.None;
        }

        internal static void SpawnBonusVillagers(SimulationState state, int nodeID, int playerID, int count)
        {
            // Count how many villagers this player currently has (including dead, excluding consumed)
            int playerVillagerCount = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].ownerID == playerID && !state.villagers[i].isConsumed)
                    playerVillagerCount++;
            }

            // Enforce per-player cap
            int maxAllowed = bal.maxVillagersPerPlayer - playerVillagerCount;
            if (maxAllowed <= 0) return;
            if (count > maxAllowed) count = maxAllowed;

            int oldLength = state.villagers.Length;
            int newLength = oldLength + count;
            VillagerData[] newArray = new VillagerData[newLength];

            for (int i = 0; i < oldLength; i++)
            {
                newArray[i] = state.villagers[i];
            }

            for (int i = 0; i < count; i++)
            {
                int newID = oldLength + i;
                newArray[newID] = new VillagerData
                {
                    villagerID = newID,
                    ownerID = playerID,
                    currentNodeID = nodeID,
                    targetNodeID = -1,
                    movePath = new int[0],
                    movePathIndex = 0,
                    moveProgress = 0,
                    previousNodeID = nodeID,
                    state = VillagerState.Idle,
                    suit = SuitType.None,
                    hp = bal.baseHP,
                    maxHP = bal.baseHP,
                    attackDamage = bal.baseAttackDamage,
                    moveSpeedTicks = bal.baseMoveSpeedTicks,
                    respawnTicksRemaining = 0,
                    attackCooldownRemaining = bal.baseAttackCooldownMax,
                    attackCooldownMax = bal.baseAttackCooldownMax,
                    combatTargetID = -1,
                    fightPriority = 0,
                    isConsumed = false,
                    productionTicksRemaining = 0,
                    productionTicksMax = 0,
                };
            }

            state.villagers = newArray;
        }

        // ===== STEP 6 =: HEALING =====

        /// <summary>
        /// Heal damaged, living, non-fighting villagers by 1 HP on their applicable
        /// interval: ordinary healing or stationary owned-Infirmary healing, once per tick.
        /// </summary>
        private static void TickHealing(SimulationState state)
        {
            bool normalDue = state.tickCount % bal.healIntervalTicks == 0;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.isConsumed) continue;
                if (v.state == VillagerState.Dead) continue;
                if (v.state == VillagerState.Fighting) continue;
                if (v.hp >= v.maxHP) continue;

                NodeData node = state.nodes[v.currentNodeID];
                bool due = normalDue;
                if (node.districtType == DistrictType.Infirmary && node.ownerID == v.ownerID && v.state != VillagerState.Moving && v.hp > 0)
                {
                    // Each Infirmary heals on its own era's interval; one with none never does.
                    int interval = bal.GetDistrictStats(DistrictType.Infirmary, node.districtEra).healIntervalTicks;
                    due = normalDue || (interval > 0 && state.tickCount % interval == 0);
                }
                if (due)
                    state.villagers[i].hp++;
            }
        }

        // ===== STEP 6: RESPAWNS =====

        private static void TickRespawns(SimulationState state, TickEventLog log)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                int decrement = bal.TimerDecrement(bal.tempoRespawnPercent, state.tickCount) + InfirmaryRespawnBoost(state, v.ownerID);
                state.villagers[i].respawnTicksRemaining -= decrement;

                if (state.villagers[i].respawnTicksRemaining <= 0)
                {
                    ResetToCore(state, i, bal, log, paid: false);
                }
            }
        }

        // Shared by paid and timer respawns so every life starts with base stats,
        // and so both announce themselves: a paid respawn runs from
        // CommandProcessor, outside SimulateTick, and would otherwise be the
        // one return nothing reported.
        internal static void ResetToCore(SimulationState state, int vid, GameBalanceData balance,
                                         TickEventLog log = null, bool paid = false)
        {
            int coreNode = state.players[state.villagers[vid].ownerID].coreNodeID;
            log?.Add(TickEventType.VillagerRespawned, coreNode, vid, state.villagers[vid].ownerID, paid ? 1 : 0);

            state.villagers[vid].state = VillagerState.Idle;
            state.villagers[vid].currentNodeID = coreNode;
            state.villagers[vid].previousNodeID = coreNode;
            state.villagers[vid].targetNodeID = -1;
            state.villagers[vid].movePath = new int[0];
            state.villagers[vid].movePathIndex = 0;
            state.villagers[vid].moveProgress = 0;
            state.villagers[vid].hp = balance.baseHP;
            state.villagers[vid].maxHP = balance.baseHP;
            state.villagers[vid].suit = SuitType.None;
            state.villagers[vid].attackDamage = balance.baseAttackDamage;
            state.villagers[vid].moveSpeedTicks = balance.baseMoveSpeedTicks;
            state.villagers[vid].attackCooldownMax = balance.baseAttackCooldownMax;
            state.villagers[vid].attackCooldownRemaining = balance.baseAttackCooldownMax;
            state.villagers[vid].combatTargetID = -1;
            state.villagers[vid].fightPriority = 0;
            state.villagers[vid].respawnTicksRemaining = 0;
            state.villagers[vid].productionTicksRemaining = 0;
            state.villagers[vid].productionTicksMax = 0;
        }

        // ===== STEP 8: WIN CONDITION =====

        private static void TickWinCondition(SimulationState state, bool[] breachedThisTick)
        {
            if (breachedThisTick != null)
            {
                int threshold = bal.BreachThresholdAt(state.tickCount);
                bool p0Loses = breachedThisTick[0] && state.players[0].breachCount >= threshold;
                bool p1Loses = breachedThisTick[1] && state.players[1].breachCount >= threshold;
                // Simultaneous losses cancel; another breach must decide the game.
                if (p0Loses != p1Loses)
                {
                    state.gameOver = true;
                    state.winnerID = p0Loses ? 1 : 0;
                }
                return;
            }
            for (int p = 0; p < state.players.Length; p++)
            {
                if (state.players[p].breachCount >= bal.breachThreshold)
                {
                    state.gameOver = true;
                    // The winner is the OTHER player (the one who breached this player's core)
                    state.winnerID = 1 - p;
                    return;
                }
            }
        }

        // ===== STEP 9: ORDER RESUME =====

        // Resolve surviving intent once, after all rule passes. Work, claim and travel
        // begun here cannot contribute until the next tick. Replan from current state.
        private static void TickOrderResume(SimulationState state, bool[] structureParticipants, TickEventLog log)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                // Destruction leaves participants Idle until the following tick.
                if (i < structureParticipants.Length && structureParticipants[i]) continue;
                if (v.state == VillagerState.Dead || v.isConsumed || v.state == VillagerState.Moving) continue;
                bool fighting = v.state == VillagerState.Fighting;
                if (HasLivingEnemiesOnNode(state, v.currentNodeID, v.ownerID)) continue;
                if (v.targetNodeID < 0 && !fighting) continue;
                if (v.state == VillagerState.Breaching && v.targetNodeID == v.currentNodeID) continue;

                v.movePath = new int[0];
                v.movePathIndex = 0;
                v.moveProgress = 0;
                v.combatTargetID = -1;
                if (v.targetNodeID >= 0 && v.targetNodeID != v.currentNodeID)
                {
                    v.movePath = Pathfinding.FindPath(state, v.ownerID, v.currentNodeID, v.targetNodeID);
                    v.state = v.movePath.Length >= 2 ? VillagerState.Moving : VillagerState.Idle;
                    state.villagers[i] = v;
                    continue;
                }
                if (v.currentNodeID == state.players[1 - v.ownerID].coreNodeID)
                {
                    EnterBreachOrProcessLegacy(state, i, v, log);
                    continue;
                }
                v.targetNodeID = -1;
                state.villagers[i] = v;
                ApplyArrivalState(state, i);
            }
        }
        private static void TickTempoEvents(int tick, TickEventLog log)
        {
            if (log == null) return;
            if (bal.tempoStageTicks != null &&
                (bal.TempoAxisValid(bal.tempoClaimPercent) || bal.TempoAxisValid(bal.tempoRespawnPercent) || bal.ProductionTempoValid()))
                for (int i = 0; i < bal.tempoStageTicks.Length; i++)
                    if (tick == bal.tempoStageTicks[i])
                        log.Add(TickEventType.TempoStage, -1, -1, -1, i);
            if (bal.BreachBarEnabled() && bal.SuddenDeathValid() && bal.suddenDeathTicks != null)
                for (int i = 0; i < bal.suddenDeathTicks.Length; i++)
                    if (tick == bal.suddenDeathTicks[i])
                        log.Add(TickEventType.SuddenDeath, -1, -1, -1, bal.suddenDeathThresholds[i]);
        }

        private static void EnterBreachOrProcessLegacy(SimulationState state, int index, VillagerData v, TickEventLog log)
        {
            if (!bal.BreachBarEnabled())
            {
                ProcessBreach(state, index, v, log);
                return;
            }
            v.state = VillagerState.Breaching;
            v.movePath = new int[0];
            v.movePathIndex = 0;
            v.moveProgress = 0;
            v.targetNodeID = v.currentNodeID;
            v.combatTargetID = -1;
            state.villagers[index] = v;
        }

        // Returns an array index, ranked by non-combat suit, HP, then villager ID.
        private static int FindBreacher(SimulationState state, int defender, out int count)
        {
            count = 0;
            int best = -1;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != 1 - defender || v.currentNodeID != state.players[defender].coreNodeID ||
                    v.state != VillagerState.Breaching || v.isConsumed) continue;
                count++;
                if (best < 0) { best = i; continue; }
                VillagerData previous = state.villagers[best];
                int suit = GameBalanceData.IsCombatSuit(v.suit) ? 1 : 0;
                int previousSuit = GameBalanceData.IsCombatSuit(previous.suit) ? 1 : 0;
                if (suit < previousSuit || (suit == previousSuit &&
                    (v.hp < previous.hp || (v.hp == previous.hp && v.villagerID < previous.villagerID))))
                    best = i;
            }
            return best;
        }

        // Inside the claiming step, before claiming so consumption frees a population slot.
        private static bool[] TickBreach(SimulationState state, int[] ownersAtTickStart, int[] resistancePercent, TickEventLog log)
        {
            if (!bal.BreachBarEnabled()) return null;
            bool[] breachedThisTick = new bool[state.players.Length];
            for (int p = 0; p < state.players.Length; p++)
            {
                int candidate = FindBreacher(state, p, out int count);
                if (count == 0)
                {
                    long decayed = (long)state.players[p].breachBar - bal.breachBarDecayPerTick;
                    state.players[p].breachBar = decayed > 0 ? (int)decayed : 0;
                    continue;
                }
                long rate = bal.breachSwarmRate[System.Math.Min(count, bal.breachSwarmRate.Length) - 1];
                rate = System.Math.Min(int.MaxValue, System.Math.Max(1,
                    checked(rate * FrontierPercent(state, state.players[p].coreNodeID, 1 - p, ownersAtTickStart)) / 100));
                rate = ResistRate(rate, resistancePercent[state.players[p].coreNodeID]);
                long progress = (long)state.players[p].breachBar + rate;
                if (progress >= bal.breachBarMax)
                {
                    ProcessBreach(state, candidate, state.villagers[candidate], log);
                    state.players[p].breachBar = 0;
                    breachedThisTick[p] = true;
                }
                else state.players[p].breachBar = (int)progress;
            }
            return breachedThisTick;
        }

        // ===== BREACH PROCESSING =====

        private static void ProcessBreach(SimulationState state, int villagerIndex, VillagerData v, TickEventLog log)
        {
            // Increment breach count for the defending player
            int defendingPlayer = 1 - v.ownerID;
            state.players[defendingPlayer].breachCount++;
            log?.Add(TickEventType.Breach, v.currentNodeID, villagerIndex, defendingPlayer, 0);

            // Consume the breaching villager permanently
            state.villagers[villagerIndex].state = VillagerState.Dead;
            state.villagers[villagerIndex].isConsumed = true;
            state.villagers[villagerIndex].hp = 0;
            state.villagers[villagerIndex].movePath = new int[0];
            state.villagers[villagerIndex].movePathIndex = 0;
            state.villagers[villagerIndex].moveProgress = 0;
            state.villagers[villagerIndex].targetNodeID = -1;
            state.villagers[villagerIndex].combatTargetID = -1;
        }

        // ===== HELPER FUNCTIONS =====

        /// <summary>
        /// Returns true if there are any living enemy villagers on the specified node.
        /// Living means: not Dead, not isConsumed.
        /// </summary>
        private static bool HasLivingEnemiesOnNode(SimulationState state, int nodeID, int myOwnerID)
        {
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.currentNodeID != nodeID) continue;
                if (vil.ownerID == myOwnerID) continue;
                if (vil.state == VillagerState.Dead) continue;
                if (vil.isConsumed) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Counts friendly villagers in Claiming state on a given node.
        /// Used for MAX_CLAIMERS_PER_NODE enforcement.
        /// </summary>
        private static int CountFriendlyClaimersOnNode(SimulationState state, int nodeID, int ownerID)
        {
            int count = 0;
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.currentNodeID != nodeID) continue;
                if (vil.ownerID != ownerID) continue;
                if (vil.state != VillagerState.Claiming) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Returns the production tick duration for a given district type.
        /// Returns 0 for non-production districts.
        /// </summary>
        private static int GetProductionTicks(DistrictType district, int era)
        {
            switch (district)
            {
                case DistrictType.Farm:
                case DistrictType.Mine:
                case DistrictType.Forge:
                    return bal.GetDistrictStats(district, era).productionTicks;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Counts friendly villagers in Working state on a given node.
        /// Used for MAX_WORKERS_PER_NODE enforcement.
        /// </summary>
        private static int CountFriendlyWorkersOnNode(SimulationState state, int nodeID, int ownerID)
        {
            int count = 0;
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.currentNodeID != nodeID) continue;
                if (vil.ownerID != ownerID) continue;
                if (vil.state != VillagerState.Working) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Returns the expected suit for a given production district.
        /// Returns SuitType.None for non-production districts.
        /// </summary>
        private static SuitType GetExpectedSuit(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Farm: return SuitType.Farmer;
                case DistrictType.Mine: return SuitType.Miner;
                case DistrictType.Forge: return SuitType.Smelter;
                case DistrictType.Infirmary: return SuitType.Acolyte;
                case DistrictType.Watchtower: return SuitType.Watcher;
                default: return SuitType.None;
            }
        }

        /// <summary>
        /// Gets the edge weight between two connected nodes.
        /// Returns state.defaultLinkWeight if no direct edge is found.
        /// Public for View layer access (interpolation).
        /// </summary>
        public static int GetLinkWeight(SimulationState state, int fromNode, int toNode)
        {
            Link[] links = state.nodes[fromNode].links;
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].toNodeID == toNode)
                    return links[i].travelWeight;
            }
            return state.defaultLinkWeight; // fallback, no direct edge found
        }

        private static int FindMostDamagedFriendly(SimulationState state, int nodeID, int ownerID, int excludeID)
        {
            int bestTarget = -1;
            int mostDamage = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (i == excludeID) continue;
                VillagerData v = state.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.ownerID != ownerID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (v.hp >= v.maxHP) continue;
                int damage = v.maxHP - v.hp;
                if (damage > mostDamage) { mostDamage = damage; bestTarget = i; }
            }
            return bestTarget;
        }

        /// <summary>
        /// A claim rate with the watchtower bonus applied, when a friendly
        /// working watchtower is adjacent. The first such tower in edge order
        /// decides the era; one tower's bonus applies, never several.
        /// </summary>
        /// <summary>
        /// How many extra ticks a dead villager of this player respawns by each
        /// tick: each working Infirmary worker adds its Infirmary era's boost.
        /// </summary>
        private static int InfirmaryRespawnBoost(SimulationState state, int playerID)
        {
            int boost = 0;
            for (int node = 0; node < state.nodes.Length; node++)
                boost += CountInfirmaryWorkers(state, node, playerID) *
                    bal.GetDistrictStats(DistrictType.Infirmary, state.nodes[node].districtEra).respawnBoostPerWorker;
            return boost;
        }

        /// <summary>Shared capped worker qualification for timers, paid respawns and UI prices.</summary>
        public static int CountInfirmaryWorkers(SimulationState state, int nodeID, int playerID)
        {
            NodeData node = state.nodes[nodeID];
            if (node.districtType != DistrictType.Infirmary || node.ownerID != playerID) return 0;
            int count = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.currentNodeID != nodeID || v.isConsumed || v.state == VillagerState.Dead || v.hp <= 0) continue;
                if (v.ownerID != playerID) return 0;
                if (v.suit == SuitType.Acolyte && v.state == VillagerState.Working && count < 2) count++;
            }
            return count;
        }

        private static bool InfirmaryWorkerSlot(SimulationState state, int villagerIndex)
        {
            VillagerData visitor = state.villagers[villagerIndex];
            int rank = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.currentNodeID != visitor.currentNodeID || v.isConsumed || v.hp <= 0 || v.state == VillagerState.Dead) continue;
                if (v.ownerID != visitor.ownerID) return false;
                if (GameBalanceData.IsCombatSuit(v.suit) ||
                    (v.state != VillagerState.Idle && v.state != VillagerState.Working && v.state != VillagerState.Claiming) ||
                    (v.targetNodeID >= 0 && v.targetNodeID != v.currentNodeID)) continue;
                if (i <= villagerIndex) rank++;
            }
            return rank <= 2;
        }
    }
}
