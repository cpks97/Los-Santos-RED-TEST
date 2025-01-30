using ExtensionsMethods;
using LosSantosRED.lsr.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class ContractorRelationship : ContactRelationship
{
    public ContractorRelationship()
    {

    }
    public ContractorRelationship(string contactName, PhoneContact phoneContact) : base(contactName, phoneContact)
    {

    }
    public override void SetupContact(IContacts contacts)
    {
        if (contacts == null)
        {
            return;
        }
        PhoneContact = contacts.PossibleContacts.ContractorContact;
    }
}