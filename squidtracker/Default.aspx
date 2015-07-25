<%@ Page Title="Squid Tracker" Language="C#" MasterPageFile="~/Main.master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="SquidTracker.Web.Default" %>
<%@ Register TagPrefix="squid" Namespace="SquidTracker.Web" Assembly="SquidTracker.Web" %>

<asp:Content ID="Content1" ContentPlaceHolderID="cpHead" runat="server">
    <squid:RequireScript Key="knockout" ScriptUrl="~/scripts/knockout-3.3.0.js" runat="server" />
    <squid:RequireScript Key="moment" ScriptUrl="~/scripts/moment.min.js" runat="server" />
    <squid:RequireScript Key="moment-timezone" ScriptUrl="~/scripts/moment-timezone-with-data.min.js" After="moment" runat="server" />
    <asp:Literal ID="litConversionScript" runat="server" />
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="cpMain" runat="server">

<script type="text/javascript">

    var squidNextStage = { "filename": "blank", "name_ja": null, "name_en": "A new map" };

    var squidViewModel =
    {
        changeTimes: [],
        mapsTurf: ko.observable([]),
        mapsRanked: ko.observable([]),
        modeRanked: ko.observable(""),
        timeEnd: ko.observable(),
        timeEndFormatted: ko.observable(),
        timeRemainingFormatted: ko.observable(),
        isStale: ko.observable(false),
        timeLastQuery: ko.observable(),
    }

    $(document).ready(function ()
    {
        ko.applyBindings(squidViewModel);
        squidRefresh();
    });

    var squidTimeout;
    function squidRefreshTimeout()
    {
        squidTimeout = setTimeout(squidRefresh, 1000);
    }

    function squidRefresh()
    {
        var mapsTurf = squidViewModel.mapsTurf();
        var timeEnd = squidViewModel.timeEnd();
        var timeLastQuery = squidViewModel.timeLastQuery();

        var timeNow = moment().utc();
        var timeRemaining, isStale, timeSinceLastQuery;

        if (timeEnd !== undefined)
        {
            timeRemaining = moment.duration(timeEnd.diff(timeNow));
            isStale = timeRemaining.asSeconds() < 0;
        }
        else
        {
            timeRemaining = moment.duration(0);
            isStale = true;
        }

        if (timeLastQuery !== undefined)
        {
            timeSinceLastQuery = moment.duration(timeNow.diff(timeLastQuery));
        }
        else
        {
            timeSinceLastQuery = moment.duration(1, 'minutes');
        }

        squidFormatTimeRemaining(timeRemaining, isStale);

        if (mapsTurf.length == 0 || (isStale && timeSinceLastQuery.asSeconds() > 5))
        {
            $.ajax("stages_info.ashx",
            {
                method: "GET",
                dataType: "json",
                success: function (data)
                {
                    var timeEnd = moment.tz(data[0].datetime_term_end, "Asia/Tokyo").utc();
                    var timeEndLocal = timeEnd.clone().local();
                    var timeNow = moment().utc();
                    var timeRemaining = moment.duration(timeEnd.diff(timeNow));
                    var isStale = timeRemaining.asSeconds() < 0;

                    squidViewModel.mapsTurf(data[0].stages);
                    squidViewModel.timeEnd(timeEnd);
                    squidViewModel.timeEndFormatted(timeEndLocal.format("LT"));
                    squidViewModel.timeLastQuery(timeNow);

                    squidFormatTimeRemaining(timeRemaining, isStale);
                },
                complete: function(data)
                {
                    squidRefreshTimeout();
                }
            });
        }
        else
        {
            squidRefreshTimeout();
        }
    }

    function squidFormatTimeRemaining(timeRemaining, isStale)
    {
        var trStr;
        if (isStale)
        {
            trStr = "0:00:00";
        }
        else
        {
            trStr = timeRemaining.hours() + ":" + padNumber(timeRemaining.minutes(), 2) + ":" + padNumber(timeRemaining.seconds(), 2);
        }
        squidViewModel.timeRemainingFormatted(trStr);
        squidViewModel.isStale(isStale);
        document.title = trStr + " - Squid Tracker";
    }

    function squidImageSrc(id)
    {
        return "images/stages/" + squidGetStage(id).filename + ".png";
    }

    function squidGetName(id)
    {
        return squidGetStage(id).name_en;
    }

    function squidGetStage(id)
    {
        var result = squidStages[id];
        if (result === undefined || result === null || result.filename == null)
            return squidNextStage;
        return result;
    }

    function padNumber(number, places)
    {
        var s = number.toString();
        while (s.length < places) { s = "0" + s; }
        return s;
    }

</script>

    <div class="squidMapColumns">
        <div class="squidLeftColumn">
            <div class="squidMapsHeading">Current Turf War maps:</div>

            <div data-bind="foreach: mapsTurf">
                <div class="squidMapPicture">
                    <img src="images/stages/blank.png" alt="" width="320" height="180"
                        data-bind="attr: { src: squidImageSrc($data.id), alt: squidGetName($data.id) }" />
                </div>
                <div class="squidMapTitle" data-bind="text: squidGetName($data.id)">

                </div>
            </div>
        </div>
        <div class="squidRightColumn">

        </div>
        <div class="squidMainColumn">
            <div data-bind="ifnot: isStale">
                Map rotation in <span data-bind="text: timeRemainingFormatted"></span>.
            </div>
            <div data-bind="if: isStale">
                Waiting for new maps...
            </div>
        </div>
    </div>
</asp:Content>
