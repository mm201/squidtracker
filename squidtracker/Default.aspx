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

    var squidNextStage = { "filename": "blank", "name_ja": null, "name_en": "A new map!" };

    var squidViewModel =
    {
        changeTimes: ko.observable([]),
        mapsTurf: ko.observable([]),
        mapsRanked: ko.observable([]),
        modeRanked: ko.observable(""),
        timeEnd: ko.observable(),
        timeEndFormatted: ko.observable(),
        timeRemainingFormatted: ko.observable(),
        isStale: ko.observable(false),
        isFresh: ko.observable(false),
        isSplatfest: ko.observable(false),
        timeNextQuery: ko.observable(),
        timeLastQuery: ko.observable(),
    }

    $(document).ready(function ()
    {
        ko.applyBindings(squidViewModel);
        squidCalcChangeTimes();
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

        var timeNow = moment().utc();
        var timeRemaining, isStale;

        if (timeEnd !== undefined)
        {
            timeRemaining = moment.duration(timeEnd.diff(timeNow));
            isStale = timeRemaining.asSeconds() < 0;
        }
        else
        {
            timeEnd = timeNow;
            timeRemaining = moment.duration(0);
            isStale = true;
        }

        squidFormatTimeRemaining(timeRemaining, isStale);
        var timeNextQuery = squidViewModel.timeNextQuery();
        if (timeNextQuery === undefined) timeNextQuery = timeNow;
        if (squidViewModel.timeLastQuery() === undefined) squidViewModel.timeLastQuery(timeNow);

        if (timeNextQuery.diff(timeNow) <= 0 && (mapsTurf.length == 0 || isStale))
        {
            $.ajax("stages_info.ashx",
            {
                method: "GET",
                dataType: "json",
                success: function (data)
                {
                    var timeEnd;

                    if (data[0].datetime_term_end === undefined ||
                        data[0].datetime_term_end == "")
                    {
                        timeEnd = moment.utc().add(-4, 'h');
                        squidViewModel.isSplatfest(true);
                    }
                    else
                    {
                        timeEnd = moment.tz(data[0].datetime_term_end, "Asia/Tokyo").utc();
                        squidViewModel.isSplatfest(false);
                    }

                    var timeEndLocal = timeEnd.clone().local();
                    var timeNow = moment().utc();
                    var timeRemaining = moment.duration(timeEnd.diff(timeNow));
                    var isStale = timeRemaining.asSeconds() < 0;

                    squidViewModel.mapsTurf(data[0].stages);
                    if (data.length > 1)
                        squidViewModel.mapsRanked(data[1].stages);
                    else
                        squidViewModel.mapsRanked(undefined);

                    squidViewModel.timeEnd(timeEnd);
                    squidViewModel.timeEndFormatted(timeEndLocal.format("LT"));

                    squidFormatTimeRemaining(timeRemaining, isStale);

                    var timeExpectedUpdate = timeEnd.clone().add(60, 's');
                    squidViewModel.timeLastQuery(timeNow);
                    squidScheduleNextUpdate(timeExpectedUpdate, timeNow);
                },
                error: function()
                {
                    var timeNow = moment().utc();
                    squidScheduleNextUpdate(squidViewModel.timeLastQuery(), timeNow);
                },
                complete: function()
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

    function squidScheduleNextUpdate(timeExpectedUpdate, timeNow)
    {
        var secondsLate = moment.duration(timeNow.diff(timeExpectedUpdate)).asSeconds();

        if (secondsLate < 0)
            squidViewModel.timeNextQuery(timeExpectedUpdate);
        else if (secondsLate < 5)
            squidViewModel.timeNextQuery(timeNow);
        else if (secondsLate < 60)
            squidViewModel.timeNextQuery(timeNow.clone().add(5, 's'));
        else
            squidViewModel.timeNextQuery(timeNow.clone().add(1800, 's'));
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
        var isSplatfest = squidViewModel.isSplatfest();
        squidViewModel.timeRemainingFormatted(trStr);
        squidViewModel.isStale(isStale && !isSplatfest);
        squidViewModel.isFresh(!isStale);
        if (isSplatfest)
            document.title = "Info unavailable - Squid Tracker";
        else if (isStale)
            document.title = "Waiting... - Squid Tracker";
        else
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

    function squidCalcChangeTimes()
    {
        var changeTimes = [];
        var firstChangeTime = moment().hours(0).minutes(0).seconds(0).utc();
        if (firstChangeTime.minutes() > 0 || firstChangeTime.seconds() > 0)
            firstChangeTime.add(1, 'h').minutes(0).seconds(0);
        var fctOffset = (firstChangeTime.hours() + 1) % 4 - 3;
        firstChangeTime.add(-fctOffset, 'h');

        changeTimes.push(firstChangeTime.clone().add(0, 'h').local().format("LT"));
        changeTimes.push(firstChangeTime.clone().add(4, 'h').local().format("LT"));
        changeTimes.push(firstChangeTime.clone().add(8, 'h').local().format("LT"));
        changeTimes.push(firstChangeTime.clone().add(12, 'h').local().format("LT"));
        changeTimes.push(firstChangeTime.clone().add(16, 'h').local().format("LT"));
        changeTimes.push(firstChangeTime.clone().add(20, 'h').local().format("LT"));
        squidViewModel.changeTimes(changeTimes);
    }

</script>

    <div class="squidMapColumns">
        <div class="squidMainAbove">
            <div class="squidStatus">
            <div data-bind="if: isFresh">
                <div>Next map rotation in</div>
                <div class="squidStatusTimeLeft" data-bind="text: timeRemainingFormatted"></div>
            </div>
            <div  data-bind="if: isStale"><div class="squidStatusWaiting"><div class="inner">
                Waiting for new maps...
            </div></div></div>
            <div  data-bind="if: isSplatfest"><div class="squidStatusWaiting"><div class="inner">
                Due to an ongoing Japanese Splatfest, map information is unavailable.
            </div></div></div>
            </div>
        </div>
        <div class="squidLeftColumn">
            <div class="squidMapsHeading squidHeadingTurf">Current Turf War maps</div>

            <div data-bind="foreach: mapsTurf">
                <div class="squidMapItem">
                <div class="squidMapPicture">
                    <img src="images/stages/blank.png" alt=""
                        data-bind="attr: { src: squidImageSrc($data.id), alt: squidGetName($data.id) }" />
                </div>
                <div class="squidMapTitle" data-bind="text: squidGetName($data.id)">

                </div>
                </div>
            </div>
            <div data-bind="if: isSplatfest">
                <div class="squidMapItem">
                    <div class="squidMapPicture">
                    <img src="images/stages/blank.png" alt="" />
                    </div>
                    <div class="squidMapTitle">Unknown</div>
                </div>
                <div class="squidMapItem">
                    <div class="squidMapPicture">
                    <img src="images/stages/blank.png" alt="" />
                    </div>
                    <div class="squidMapTitle">Unknown</div>
                </div>
            </div>
        </div>
        <div class="squidRightColumn">
            <div class="squidMapsHeading squidHeadingRanked">Current Ranked maps</div>

            <div data-bind="foreach: mapsRanked">
                <div class="squidMapItem">
                <div class="squidMapPicture">
                    <img src="images/stages/blank.png" alt=""
                        data-bind="attr: { src: squidImageSrc($data.id), alt: squidGetName($data.id) }" />
                </div>
                <div class="squidMapTitle" data-bind="text: squidGetName($data.id)">

                </div>
                </div>
            </div>
            <div data-bind="if: isSplatfest">
                <div class="squidMapItem">
                    <div class="squidMapPicture">
                    <img src="images/stages/blank.png" alt="" />
                    </div>
                    <div class="squidMapTitle">Unknown</div>
                </div>
                <div class="squidMapItem">
                    <div class="squidMapPicture">
                    <img src="images/stages/blank.png" alt="" />
                    </div>
                    <div class="squidMapTitle">Unknown</div>
                </div>
            </div>
        </div>
        <div class="squidMainColumn">
            <div>Your map rotation times are</div>
            <ul class="squidChangeTimes" data-bind="foreach: changeTimes">
                <li class="squidChangeTime" data-bind="text: $data"></li>
            </ul>
        </div>
    </div>
</asp:Content>
