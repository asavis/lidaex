/*
 *   Usage:
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

Ext.onReady(function () {

    Ext.QuickTips.init();

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

    const store = Ext.create("Ext.data.Store", {
        model: "StandingsModel",
        proxy: {
            type: "ajax",
            url: StandingFileName,
            reader: "json"
        },
        autoLoad: true
    });

    const grid = Ext.create("Ext.grid.Panel", {
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
                    const rank = record.data["Rank"];
                    if (rank > 3) return value;
                    return value + ' <img src="' + RanksIcons[rank - 1] + '" width="22" alt="rank ' + rank + '" style="float:right" />';
                }
            },
            {
                text: "Команда",
                flex: 1,
                minWidth: 220,
                sortable: true,
                dataIndex: "Name",
                renderer: function (value, metaData, record) {
                    metaData.tdStyle = "white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
                    const name = Ext.String.htmlEncode(value || "");
                    metaData.tdAttr = 'data-qtip="' + name + '"';
                    const id = encodeURIComponent(record.data["Id"] || "");
                    return '<a href="https://lichess.org/team/' + id + '" target="_blank" rel="noopener">' + name + '</a>';
                }
            },
            {
                text: "Турніри",
                width: 220, 
                minWidth: 160,
                sortable: false,
                align: "left",
                dataIndex: "Results",
                renderer: function (value, metaData, record) {
                    metaData.tdStyle = "white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";
                    if (!Ext.isArray(value) || value.length === 0) return "";

                    let txt = "";
                    for (let i = 0; i < value.length; i++) {
                        const item = value[i] || {};
                        if (i !== 0) {
                            txt += (item.ResultType === "adjustment" && typeof item.Value === "number")
                                ? (item.Value >= 0 ? " + " : " ")
                                : " + ";
                        }
                        if (item.ResultType === "adjustment") {
                            const comment = Ext.String.htmlEncode(item.Comment || "");
                            const valText = (typeof item.Value === "number")
                                ? ((item.Value > 0 ? "+" : "") + item.Value)
                                : Ext.String.htmlEncode(String(item.Value || ""));
                            txt += '<span style="color:red;" data-qtip="' + comment + '">' + valText + '</span>';
                            continue;
                        }

                        const d = item.TournamentDate ? new Date(item.TournamentDate) : null;
                        const dText = (d && !isNaN(d.getTime())) ? Ext.Date.format(d, "d.m.Y") : "";
                        const league = Ext.String.htmlEncode(item.LeagueName || "");
                        const rankText = (item.Rank != null) ? String(item.Rank) : "";
                        const tip = ('[' + rankText + '] ' + league + ' ' + dText).split(' ').join('&nbsp;');
                        const qtip = Ext.String.htmlEncode(tip);
                        const lichessId = encodeURIComponent(item.LichessTournamentId || "");
                        const scoreText = (item.Score != null) ? String(item.Score) : "";
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
        },

        renderTo: "extjs_grid"
    });
});
