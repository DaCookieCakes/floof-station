interaction-LookAt-name = Look at
interaction-LookAt-description = Stare into the void and see it stare back.
interaction-LookAt-success-self-popup = You look at {THE($target)}.
interaction-LookAt-success-target-popup = You feel {THE($user)} looking at you...
interaction-LookAt-success-others-popup = {THE($user)} looks at {THE($target)}.

interaction-BreastFeed-name = Breast feed
interaction-BreastFeed-description = suckle on their voluptuous breasts
interaction-BreastFeed-success-self-popup = You suckle on {THE($target)} breasts.
interaction-BreastFeed-success-target-popup = You feel {THE($user)} sucking on your breasts
interaction-BreastFeed-success-others-popup = {THE($user)} sucks at {THE($target)} breasts.

interaction-SuckDick-name = Suck dick
interaction-SuckDick-description = Wrap your lips around their cock.
interaction-SuckDick-success-self-popup = You suck on {THE($target)} cock.
interaction-SuckDick-success-target-popup = You feel {THE($user)} sucking on your cock.
interaction-SuckDick-success-others-popup = {THE($user)} sucks {THE($target)} cock.

interaction-EatOut-name = Eat out
interaction-EatOut-description = Bury your face between their thighs.
interaction-EatOut-success-self-popup = You lick at {THE($target)} pussy.
interaction-EatOut-success-target-popup = You feel {THE($user)} licking at your pussy.
interaction-EatOut-success-others-popup = {THE($user)} eats out {THE($target)}.

interaction-Rim-name = Rim
interaction-Rim-description = Get your tongue right in there.
interaction-Rim-success-self-popup = You rim {THE($target)}.
interaction-Rim-success-target-popup = You feel {THE($user)} tongue on your rear.
interaction-Rim-success-others-popup = {THE($user)} rims {THE($target)}.

# Designed to not be seen by others, only you and your target.  Plays a light sound effect that is very unique to it to catch the attention of the party being eye'd up.
interaction-CheckOut-name = Check out
interaction-CheckOut-description = This lets you check someone out on the down low, only you and they will know you did.
interaction-CheckOut-success-self-popup = You are really eyeballing {THE($target)}.
interaction-CheckOut-success-target-popup = You think that {THE($user)} might be checking you out...

interaction-Hug-name = Hug
interaction-Hug-description = A hug a day keeps the psychological horrors beyond your comprehension away.
interaction-Hug-success-self-popup = You hug {THE($target)}.
interaction-Hug-success-target-popup = {THE($user)} hugs you.
interaction-Hug-success-others-popup = {THE($user)} hugs {THE($target)}.

interaction-Pet-name = Pet
interaction-Pet-description = Pet your co-worker to ease their stress.
interaction-Pet-success-self-popup = You pet {THE($target)} on {POSS-ADJ($target)} head.
interaction-Pet-success-target-popup = {THE($user)} pets you on your head.
interaction-Pet-success-others-popup = {THE($user)} pets {THE($target)}.

interaction-PetAnimal-name = {interaction-Pet-name}
interaction-PetAnimal-description = Pet an animal.
interaction-PetAnimal-success-self-popup = {interaction-Pet-success-self-popup}
interaction-PetAnimal-success-target-popup = {interaction-Pet-success-target-popup}
interaction-PetAnimal-success-others-popup = {interaction-Pet-success-others-popup}

interaction-KnockOn-name = Knock
interaction-KnockOn-description = Knock on the target to attract attention.
interaction-KnockOn-success-self-popup = You knock on {THE($target)}.
interaction-KnockOn-success-target-popup = {THE($user)} knocks on you.
interaction-KnockOn-success-others-popup = {THE($user)} knocks on {THE($target)}.

interaction-Rattle-name = Rattle
interaction-Rattle-success-self-popup = You rattle {THE($target)}.
interaction-Rattle-success-target-popup = {THE($user)} rattles you.
interaction-Rattle-success-others-popup = {THE($user)} rattles {THE($target)}.

# The below includes conditionals for if the user is holding an item
interaction-WaveAt-name = Wave at
interaction-WaveAt-description = Wave at the target. If you are holding an item, you will wave it.
interaction-WaveAt-success-self-popup = You wave {$hasUsed ->
    [false] at {THE($target)}.
    *[true] your {$used} at {THE($target)}.
}
interaction-WaveAt-success-target-popup = {THE($user)} waves {$hasUsed ->
    [false] at you.
    *[true] {POSS-PRONOUN($user)} {$used} at you.
}
interaction-WaveAt-success-others-popup = {THE($user)} waves {$hasUsed ->
    [false] at {THE($target)}.
    *[true] {POSS-PRONOUN($user)} {$used} at {THE($target)}.
}

interaction-Kiss-name = Kiss
interaction-Kiss-description = A kiss to melt the pain away. Requires your mouth to be free.
interaction-Kiss-success-self-popup = You kiss {THE($target)}.
interaction-Kiss-success-target-popup = {THE($user)} kisses you.
interaction-Kiss-success-others-popup = {THE($user)} kisses {THE($target)}.

interaction-Lick-name = Lick
interaction-Lick-description = Lick your co-worker. Requires your mouth to be free.
interaction-Lick-success-self-popup = You lick {THE($target)}.
interaction-Lick-success-target-popup = {THE($user)} licks you.
interaction-Lick-success-others-popup = {THE($user)} licks {THE($target)}.
