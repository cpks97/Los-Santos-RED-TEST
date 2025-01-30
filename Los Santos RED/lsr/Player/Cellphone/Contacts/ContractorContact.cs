using LosSantosRED.lsr.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class ContractorContact : PhoneContact
{




    public ContractorContact()
    {

    }

    public ContractorContact(string name) : base(name)
    {

    }

    public override void OnAnswered(IContactInteractable player, CellPhone cellPhone, IGangs gangs, IPlacesOfInterest placesOfInterest, ISettingsProvideable settings,
        IJurisdictions jurisdictions, ICrimes crimes, IEntityProvideable world, IModItems modItems, IWeapons weapons, INameProvideable names, IShopMenus shopMenus, IAgencies agencies)
    {
        MenuInteraction = new ContractorInteraction(player, gangs, placesOfInterest, settings, this, agencies);
        MenuInteraction.Start(this);
    }
    public override ContactRelationship CreateRelationship()
    {
        return new ContractorRelationship(Name, this);
    }

    internal void Add(ContractorContact contractorContact)
    {
        throw new NotImplementedException();
    }
}

