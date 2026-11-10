using Roamly.Api.Features.Places;

namespace Roamly.Model.Tests;

public sealed class GoogleMapsPositionTests
{
    [Theory]
    [InlineData("https://www.google.com/maps/place/Bunker/@42.2370291,12.5091909,805m/data=!3m2!1e3!8m2!3d42.2370252!4d12.5117658!16s%2Fg%2F11gbfd5xfy", 42.2370252, 12.5117658)]
    [InlineData("https://www.google.it/maps/search/?api=1&query=0%2C-180", 0, -180)]
    [InlineData("https://maps.google.com/?q=0,-180", 0, -180)]
    [InlineData("https://www.google.com/maps/data=%213d0%214d-180", 0, -180)]
    public void Explicit_place_coordinates_are_extracted(string url, double latitude, double longitude)
    {
        var position = GoogleMapsPosition.Parse(url);
        Assert.Equal((decimal)latitude, position?.Latitude);
        Assert.Equal((decimal)longitude, position?.Longitude);
    }

    [Theory]
    [InlineData("https://google.com.evil.test/maps/?q=1,2")]
    [InlineData("https://evil@google.com/maps/?q=1,2")]
    [InlineData("https://www.google.com/maps/@1,2,12z")]
    [InlineData("https://maps.app.goo.gl/example")]
    [InlineData("https://www.google.com/maps/data=!3d1!4d2!3d3!4d4")]
    [InlineData("https://www.google.com/maps/data=!3d1")]
    [InlineData("https://www.google.com/maps/data=!4d2")]
    [InlineData("https://www.google.com/maps/data=!3d91!4d2")]
    [InlineData("https://www.google.com/maps/?q=NaN,2")]
    [InlineData("https://www.google.com/maps/?q=1,Infinity")]
    [InlineData("https://www.google.com/maps/?q=1,181")]
    [InlineData("https://www.google.com/maps/?q=Place+1,2")]
    [InlineData("https://www.google.com/maps/?q=1,2&q=3,4")]
    [InlineData("https://www.google.com/maps/?q=1,2&query=3,4&api=1")]
    [InlineData("https://www.google.com/maps/?q=%2531%252C2")]
    [InlineData("https://www.google.com/maps/?q=%zz")]
    [InlineData("https://www.google.com/maps/data=!3d1!4d2?q=3,4")]
    [InlineData("http://www.google.com/maps/?q=1,2")]
    [InlineData("not a URL")]
    [InlineData("https:////google.com/maps/?q=1,2")]
    [InlineData("https://%67oogle.com/maps/?q=1,2")]
    [InlineData("https://google.com/maps/%zz/data=!3d1!4d2")]
    [InlineData("https://www.google.com/maps/dir/First/Second/data=!3d1!4d2")]
    [InlineData("https://www.google.com/maps/?query=1,2")]
    [InlineData("https://www.google.com/maps/?api=1&api=2&query=1,2")]
    public void Unsupported_or_ambiguous_links_do_not_invent_coordinates(string url)
    {
        Assert.Null(GoogleMapsPosition.Parse(url));
    }
}
