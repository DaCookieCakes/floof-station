live-character-picker-badge = Live
live-character-picker-badge-tooltip = This character has a live version, which spawns instead of the template.
live-character-preview-label = Live version
live-character-preview-label-round = Live version · last saved round {$round}
live-character-show-template = Show template
live-character-editor-warning = [color=yellow]{$name} has a live version, which spawns instead of this template. Edits apply once you discard it.[/color]
live-character-discard = Discard live version
live-character-discard-confirm = Discard for good?

verb-categories-spawn-here = Spawn here

admin-player-actions-spawn-floof = {$live ->
    [true] Live version
   *[false] Template
} ({$tracked ->
    [true] tracked
   *[false] untracked
})
admin-player-actions-spawn-floof-message = Spawns a new copy of this character and immediately transfers their mind to it. {$tracked ->
    [true] It becomes the character and is saved if it makes it to CC.
   *[false] It's only a copy and is never saved.
}
