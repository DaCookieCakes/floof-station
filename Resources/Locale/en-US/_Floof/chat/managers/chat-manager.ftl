# Floof-start: subtle

chat-manager-entity-subtle-wrap-message = [italic]{ PROPER($entity) ->
    *[false] the {$entityName} subtly {$message}[/italic]
     [true] {$entityName} subtly {$message}[/italic]
    }

chat-manager-entity-subtle-ooc-wrap-message = [italic]{ PROPER($entity) ->
    *[false] [color=pink]the {$entityName} sooc: {$message}[/color][/italic]
     [true] [color=pink]{$entityName} sooc: {$message}[/color][/italic]
    }
