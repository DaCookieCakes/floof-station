// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
namespace Content.Shared._Floof.Traits.Assorted;

public abstract class SharedRedshirtSystem : EntitySystem
{
    public bool IsRedshirt(Entity<RedshirtComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return false;

        return true;
    }
}
