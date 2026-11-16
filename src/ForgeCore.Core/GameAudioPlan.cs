namespace ForgeCore.Core;

public sealed record GameAudioCue(string Id,string Category,string? ProceduralCandidate,string AcceptanceGate);
public sealed record GameAudioPlan(string GenreId,IReadOnlyList<GameAudioCue> Cues,bool RequirementsOnly,IReadOnlyList<string> OpenGates);

/// <summary>Genre/system-driven requirements, not an assertion that every classified genre has a runtime builder.</summary>
public static class GameAudioPlanner
{
    public static GameAudioPlan Create(GameBlueprint blueprint)
    {
        blueprint.Validate();var systems=blueprint.SystemPackIds;var cues=new List<GameAudioCue>();
        void Add(string id,string category,string? candidate=null)=>cues.Add(new(id,category,candidate,"Human listening/mix plus the genre's real gameplay event/timing tests."));
        Add("ui_confirm","interface","ui");Add("ambient_loop","environment","ambient");
        if(systems.Contains("locomotion_2d")||systems.Contains("locomotion_3d"))Add("grounded_footsteps","locomotion","surface_footstep_v2");
        if(systems.Contains("combat_ranged")){Add("weapon_shot","combat","layered_shot_v2");Add("weapon_reload","combat","reload");Add("projectile_impact","combat","impact");}
        if(systems.Contains("combat_melee")){Add("melee_swing","combat");Add("melee_contact","combat","impact");}
        if(systems.Contains("vehicle_physics")){Add("vehicle_engine","vehicle");Add("tyre_surface","vehicle");Add("vehicle_collision","vehicle","impact");}
        if(systems.Contains("rhythm_timing")){Add("synchronized_music_transport","rhythm");Add("timed_beat_judgement","rhythm");}
        if(systems.Contains("puzzle_logic"))Add("puzzle_state_change","puzzle");
        if(systems.Contains("strategy_units")){Add("unit_selection","strategy");Add("unit_order_confirmation","strategy");}
        if(systems.Contains("economy_simulation"))Add("economy_event","simulation");
        if(systems.Contains("dialogue_quests"))Add("dialogue_presentation","narrative");
        if(systems.Contains("inventory"))Add("inventory_pickup","items");
        return new(blueprint.PrimaryGenreId,cues,true,[
            "Only the industrial FPS demo currently integrates the layered/surface bank; generic campaign builders do not satisfy this plan automatically.",
            "Missing genre recipes, gameplay systems and event adapters remain explicit; no FPS substitution for another genre.",
            "External recordings/models/downloads require explicit source rights/provenance review; local synthesis is not listening acceptance.",
            "Music composition/transport, dialogue voice, advanced acoustics and release quality are not implied by cue planning."]);
    }
}
