/*
 *   Usage:
 *
 *   <link rel="stylesheet" type="text/css" href="https://cdn.sencha.com/ext/gpl/4.2.1/resources/ext-theme-neptune/ext-theme-neptune-all.css"/>
 *   <script type="text/javascript" src="https://cdn.sencha.com/ext/gpl/4.2.1/ext-all.js"></script>
 *   <script type="text/javascript" src="/standings/standings.js"></script>
 *
 *   <div id="extjs_grid"> 
 */

Ext.require([
    "Ext.grid.*",
    "Ext.data.*",
    "Ext.util.*"
]);

var StandingFileName = "/standings/standings.json";
var RanksIcons = [
    "/standings/trophy.png",
    "/standings/medal2.png",
    "/standings/medal3.png"
];

Ext.override(Ext.grid.View, { enableTextSelection: true });

Ext.onReady(function() {

    Ext.QuickTips.init();

    Ext.define("StandingsModel",
        {
            extend: "Ext.data.Model",
            fields: [
                { name: "Id", type: "string" },
                { name: "Name", type: "string" },
                { name: "Rank", type: "int" },
                { name: "Score", type: "float" },
                { name: "LichessScore", type: "int" },
                { name: "Results" }
            ]
        });

    const store = Ext.create("Ext.data.Store",
        {
            model: "StandingsModel",
            proxy: {
                type: "ajax",
                url: StandingFileName,
                reader: "json"
            },
            autoLoad: true
        });

    Ext.create("Ext.grid.Panel",
        {
            store: store,
            columns: [
                {
                    text: "Місце",
                    sortable: true,
                    width: 75,
                    dataIndex: "Rank",
                    renderer: function(value, metaData, record, row, col, store, gridView) {
                        const rank = record.data["Rank"];
                        if (rank > 3) return value;
                        return value + `<img src="${RanksIcons[rank - 1]}" width=22 align=right />`;
                    }
                },
                {
                    text: "Команда",
                    flex: 1,
                    width: 140,
                    sortable: true,
                    dataIndex: "Name",
                    renderer: function(value, metaData, record, row, col, store, gridView) {
                        return `<a href="https://lichess.org/team/${record.data["Id"]}" target="_blank">${value}</a>`;
                    }
                },
                {
                    text: "Турніри",
                    flex: 1,
                    sortable: false,
                    align: "left",
                    dataIndex: "Results",
                    renderer: function(value, metaData, record, row, col, store, gridView) {
                        var txt = "";

                        for (i = 0; i < value.length; i++) {
                            if (i !== 0) txt += " + ";

                            let tip = `[${value[i].Rank}] ${value[i].LeagueName} ${Ext.Date.format(
                                Ext.Date.parse(value[i].TournamentDate, "c"),
                                "d.m.Y")}`;

                            tip = tip.replaceAll(" ", "&nbsp");
                            txt += `<a href="https://lichess.org/tournament/${value[i].LichessTournamentId
                                }" target="_blank" data-qtip="${tip}">${value[i].Score}</a>`;
                        }

                        return txt;
                    }
                },
                {
                    text: "Очки",
                    sortable: true,
                    width: 100,
                    xtype: "numbercolumn",
                    format: "0.0",
                    align: "right",
                    dataIndex: "Score"
                },
                {
                    text: "Очки Lichess",
                    sortable: true,
                    width: 100,
                    align: "right",
                    dataIndex: "LichessScore"
                }
            ],

            height: 500,

            viewConfig: {
                stripeRows: true
            },

            renderTo: "extjs_grid"
        });
});