using System;
using System.Collections.Generic;
using System.Text;

namespace DraftView.Domain.Enumerations
{
    public enum DisplayTheme
    {
        // Adventure theme (default) — matches legacy "Light" / "Dark" DB values
        Light               = 0,
        Dark                = 1,

        // Crime / Glasgow Rain
        CrimeLight          = 2,
        CrimeDark           = 3,

        // Noir / Cold War Spy
        NoirLight           = 4,
        NoirDark            = 5,

        // Contemporary
        ContemporaryLight   = 6,
        ContemporaryDark    = 7,

        // Historic / Georgian
        HistoricLight       = 8,
        HistoricDark        = 9,
    }
}
