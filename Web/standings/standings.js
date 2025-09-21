/*
 * Usage:
 *
 *   <link rel="stylesheet" type="text/css" href="https://cdn.sencha.com/ext/gpl/4.2.1/resources/ext-theme-neptune/ext-theme-neptune-all.css"/>
 *   <script type="text/javascript" src="https://cdn.sencha.com/ext/gpl/4.2.1/ext-all.js"></script>
 *   <script type="text/javascript" src="/standings/standings.js"></script>
 *
 *   <div id="extjs_grid"></div>
 */

Ext.require([
    "Ext.grid.*",
    "Ext.data.*",
    "Ext.util.*",
    "Ext.panel.Panel",
    "Ext.layout.container.Auto"
]);

var StandingFileName = "/standings/standings.json";
var RanksIcons = [
    "/standings/trophy.png",
    "/standings/medal2.png",
    "/standings/medal3.png"
];

Ext.override(Ext.grid.View, { enableTextSelection: true });

function InitTooltips() {
    Ext.QuickTips.init();

    var host = Ext.get('extjs_grid'); if (!host) return;

    var probe = Ext.getBody().createChild({
        tag: 'div',
        cls: 'x-tip-body-default',
        style: 'position:absolute;left:-10000px;top:-10000px;visibility:hidden;' +
            'white-space:normal;word-break:break-word;line-height:1.25;'
    });

    function measure(html, minW, maxW) {
        probe.update(html || '');
        probe.setStyle('width', 'auto');
        var w = probe.dom.offsetWidth;
        if (w < minW) w = minW;
        if (w > maxW) w = maxW;
        return w;
    }

    var tip = Ext.create('Ext.tip.ToolTip', {
        target: host,
        delegate: 'span[data-comment]'
    });

    tip.on('beforeshow', function (t) {
        var el = t.triggerElement;
        if (!el) return false;

        var raw = el.getAttribute('data-comment') || '';
        t.update(raw);

        var w = measure(raw, t.minWidth, t.maxWidth);
        t.setWidth(w);
    });
}

Ext.onReady(function () {
    InitTooltips();

    Ext.define("StandingsModel", {
        extend: "Ext.data.Model",
        fields: [
            { name: "Id", type: "string" },
            { name: "Name", type: "string" },
            { name: "Rank", type: "int" },
            { name: "Score", type: "float" },
            { name: "LichessScore", type: "int" },
            { name: "Results" } // Array
        ]
    });

    var store = Ext.create("Ext.data.Store", {
        model: "StandingsModel",
        proxy: {
            type: "ajax",
            url: StandingFileName,
            reader: "json"
        },
        autoLoad: true
    });

    var grid = Ext.create("Ext.grid.Panel", {
        renderTo: "extjs_grid",
        store: store,
        forceFit: true,
        height: 594,
        border: 1,

        columns: [
            {
                text: "Місце",
                sortable: true,
                width: 60,
                dataIndex: "Rank",
                renderer: function (value, metaData, record) {
                    var rank = record.data["Rank"];
                    if (rank > 3) return value;
                    return value + ' <img src="' + RanksIcons[rank - 1] + '" width="22" alt="rank ' + rank + '" style="float:right" />';
                }
            },
            {
                text: "Команда",                
                width: 220,
                sortable: true,
                dataIndex: "Name",
                renderer: function (value, metaData, record) {
                    metaData.tdStyle = "white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
                    var name = Ext.String.htmlEncode(value || "");
                    metaData.tdAttr = 'data-qtip="' + name + '"';
                    var id = encodeURIComponent(record.data["Id"] || "");
                    return '<a href="https://lichess.org/team/' + id + '" target="_blank" rel="noopener">' + name + '</a>';
                }
            },
            {
                text: "Турніри",
                flex: 1,
                
                minWidth: 160,
                sortable: false,
                align: "left",
                dataIndex: "Results",
                renderer: function (value, metaData, record) {
                    metaData.tdStyle = "white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
                    if (!Ext.isArray(value) || value.length === 0) return "";

                    var txt = "";
                    for (var i = 0; i < value.length; i++) {
                        var item = value[i] || {};

                        if (i !== 0) {
                            if (item.LichessTournamentId === "adjustment" && typeof item.Score === "number") {     
                                sign = item.Score >= 0 ? " + " : " - ";
                                txt += '<span style="color:red;">' + sign + '</span>'; ;
                            } else {         
                                txt += " + ";
                            }
                        }

                        if (item.LichessTournamentId === "adjustment") {
                            var comment = String(item.LeagueName || '');
                            var valText = Ext.util.Format.number(Math.abs(item.Score || 0), "0.##");

                            if (item.LichessTournamentId === "adjustment") {
                                txt += '<span style="color:red;" data-comment="' + comment + '">' + valText + '</span>';
                                continue;
                            }
                        }

                        var d = item.TournamentDate ? new Date(item.TournamentDate) : null;
                        var dText = (d && !isNaN(d.getTime())) ? Ext.Date.format(d, "d.m.Y") : "";
                        var league = Ext.String.htmlEncode(item.LeagueName || "");
                        var rankText = (item.Rank != null) ? String(item.Rank) : "";
                        var tip = ('[' + rankText + '] ' + league + ' ' + dText).split(' ').join('&nbsp;');
                        var qtip = Ext.String.htmlEncode(tip);
                        var lichessId = encodeURIComponent(item.LichessTournamentId || "");
                        var scoreText = (item.Score != null) ? String(item.Score) : "";
                        txt += '<a href="https://lichess.org/tournament/' + lichessId + '" target="_blank" rel="noopener" data-qtip="' + qtip + '">' + scoreText + '</a>';
                    }
                    return txt;
                }
            },
            {
                text: "Очки",
                tooltip: "Сума очок з бонусами та штрафами",
                sortable: true,
                width: 66,
                xtype: "numbercolumn",
                format: "0.0",
                align: "right",
                dataIndex: "Score"
            },
            {
                text: "Lichess",
                tooltip: "Сума очок Lichess",
                width: 66,
                align: "right",
                dataIndex: "LichessScore"
            }
        ],

        viewConfig: {
            stripeRows: true
        }
    });
});
