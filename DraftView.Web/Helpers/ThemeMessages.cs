namespace DraftView.Web.Helpers;

public static class ThemeMessages
{
    private static string ThemeFamily(string theme) => theme switch
    {
        "CrimeLight" or "CrimeDark"                     => "Crime",
        "NoirLight"  or "NoirDark"                      => "Noir",
        "ContemporaryLight" or "ContemporaryDark"        => "Contemporary",
        "HistoricLight"     or "HistoricDark"            => "Historic",
        _                                               => "Adventure",
    };

    public static string Get403(string theme) => ThemeFamily(theme) switch
    {
        "Crime"        => "Hold it right there. You don't have clearance for this area.",
        "Noir"         => "Your credentials don't open this door. Move along.",
        "Contemporary" => "You don't have access to this page.",
        "Historic"     => "Entry to this chamber is not permitted without proper authority.",
        _              => "These paths are closed to you — venture no further without proper leave.",
    };

    public static string Get404(string theme) => ThemeFamily(theme) switch
    {
        "Crime"        => "We've turned every corner — there's nothing here. The trail's gone cold.",
        "Noir"         => "The file you're looking for has gone cold. No trace, no trail.",
        "Contemporary" => "We couldn't find what you were looking for.",
        "Historic"     => "The document you seek has been lost to the archives, or never existed.",
        _              => "The trail you've chosen slips beyond the borders of any realm we can enter.",
    };

    public static string Get405(string theme) => ThemeFamily(theme) switch
    {
        "Crime"        => "That's not how we do things round here. Try a different approach.",
        "Noir"         => "That method's blown. Use a different approach.",
        "Contemporary" => "That request method isn't supported here.",
        "Historic"     => "Your method of approach is not one we can accommodate.",
        _              => "That approach holds no passage here. Try a different route.",
    };

    public static string Get500(string theme) => ThemeFamily(theme) switch
    {
        "Crime"        => "Something went sideways. The system couldn't handle this one.",
        "Noir"         => "The operation failed. We're not sure why, and we're not asking.",
        "Contemporary" => "Something went wrong. We're looking into it.",
        "Historic"     => "Something went amiss in the workings of this machine.",
        _              => "Something broke the spell. The system could not complete your journey.",
    };
}
