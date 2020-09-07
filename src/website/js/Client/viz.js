async function drawScorigamiViz() {
    // ----------------------
    // 1. Access data

    const dataset = await d3.json("/data")
    const games = dataset.games.filter(d => d.pts_win > 25) // TODO: Remove when data is cleaned
    const lastupdated = dataset.lastUpdated
    const yAccessor = d => d.pts_win
    const xAccessor = d => d.pts_lose
    const dateAccessor = d => new Date(d.first_date)
    const countAccessor = d => d.count
    var currentMetric = "date"

    const firstgamewinningteam = d => d.first_team_win
    const firstgamelosingteam = d => d.first_team_lose
    const latestgamewinningteam = d => d.last_team_win
    const latestgamelosingteam = d => d.last_team_lose

    const minScoreInView = 30
    const maxScoreInView = d3.max(games, yAccessor)
    const nbaOrange = "#FA4D01"
    const fadednbaOrange = "#f5cc98"
    const yearFormat = d3.timeFormat("%Y")
    const monthdayyearFormat = d3.timeFormat("%b %d, %Y")

    const totalnumberofscorigamis = games.length
    const mostrecentscorigami = games[d3.maxIndex(games, dateAccessor)]
    const mostrecentscorigamidate = monthdayyearFormat(new Date(mostrecentscorigami.first_date))

    // Create impossible scores data
    var impossiblescores = []
    var possiblescores = []

    for (i = minScoreInView; i <= d3.max(games, yAccessor); i++) {
        for (j = minScoreInView; j <= d3.max(games, xAccessor);j++) {
            if (i<=j){
                impossiblescores.push({"pts_win" : i, "pts_lose" : j})
            } else {
                possiblescores.push({"pts_win" : i, "pts_lose" : j})
            }
        }
    }

    // ----------------------
    // 2. Create chart dimensions

    let dimensions = {
        width: window.innerHeight * 0.85, // height so that it is square
        height: window.innerHeight * 0.85,
        margin: {
          top: 15,
          right: 15,
          bottom: 60,
          left: 60,
        },
      }
      dimensions.boundedWidth = dimensions.width
        - dimensions.margin.left
        - dimensions.margin.right
      dimensions.boundedHeight = dimensions.height
        - dimensions.margin.top
        - dimensions.margin.bottom

    // ----------------------
    // 3. Draw canvas

    const wrapper = d3.select(".viz")
    .append("svg")
        .attr("width", dimensions.width)
        .attr("height", dimensions.height)

    const bounds = wrapper.append("g")
        .style("transform", `translate(${
        dimensions.margin.left
        }px, ${
        dimensions.margin.top
        }px)`)

    // ----------------------    
    // 4. Create scales

    const yScale = d3.scaleBand()
        .domain(d3.range(minScoreInView, d3.max(games, yAccessor) + 1))
        .range([dimensions.boundedHeight, 0])
        .paddingInner(0.1)
    
    const xScale = d3.scaleBand()
        .domain(d3.range(minScoreInView, d3.max(games, xAccessor) + 1))
        .range([0, dimensions.boundedHeight]) // Using boundedHeight instead of boundedWeight for square marks and viz
        .paddingInner(0.1)

    // ----------------------
    // 5. Draw data

    // 5c. Possible scores
    bounds.selectAll(".possiblescores")
        .data(possiblescores)
        .join("rect")
        .attr("class", "possiblescores")
        .attr("x", d => xScale(xAccessor(d)))
        .attr("y", d => yScale(yAccessor(d)))
        .attr("width", xScale.bandwidth())
        .attr("height", yScale.bandwidth())
        .attr("fill", "white")
        .on("mouseenter", onMouseEnterBlankspace)
        .on("mouseleave", onMouseLeaveBlankspace)
        .on("touchstart", onMouseEnterBlankspace)
        .on("touchmove", onMouseEnterBlankspace)
    
    // 5a. Heatmap recs
    function drawHeatMapRects(metric) {
        currentMetric = metric

        const colorAccessor = currentMetric === "date" ? dateAccessor : countAccessor

        const colorRangeDomain = d3.extent(games, colorAccessor)
        const colorRange = d3.scaleLinear()
            .domain(colorRangeDomain)
            .range([0, 1])
            .clamp(true)
        const colorGradient = d3.interpolateHcl(fadednbaOrange, nbaOrange)
        const colorScale = d => colorGradient(colorRange(d) || 0)

        bounds.selectAll(".scorigamiscores")
            .data(games)
            .join("rect")
            .attr("class", "scorigamiscores")
            .attr("x", d => xScale(xAccessor(d)))
            .attr("y", d => yScale(yAccessor(d)))
            .attr("width", xScale.bandwidth())
            .attr("height", yScale.bandwidth())
            .attr("fill", d => colorScale(colorAccessor(d)))
            .on("mouseenter", onMouseEnter)
            .on("mouseleave", onMouseLeave)
            .on("touchstart", onMouseEnter)
            .on("touchmove", onMouseEnter)

        var legendMinLabel = ""
        var legendMaxLabel = ""
        var legendTitle = currentMetric === "date" ? "Year of original scorigami game" : "Number of games"

        if (currentMetric === "date") {
            legendMinLabel = yearFormat(colorRangeDomain[0])
            legendMaxLabel = yearFormat(colorRangeDomain[1])
        } else {
            legendMinLabel = colorRangeDomain[0]
            legendMaxLabel = colorRangeDomain[1]
        }
        d3.select(".legend-title")
            .text(legendTitle)
        d3.select("#legend-min")
            .text(legendMinLabel)
        d3.select("#legend-max")
            .text(legendMaxLabel)
        d3.select("#legend-gradient")
            .style("background", `linear-gradient(to right, ${
            new Array(10).fill(null).map((d, i) => (
                `${colorGradient(i / 9)} ${i * 100 / 9}%`
            )).join(", ")
            })`)
    }
    drawHeatMapRects(currentMetric)

    // 5b. Impossible scores
    bounds.selectAll(".impossiblescores")
        .data(impossiblescores)
        .join("rect")
        .attr("class", "impossiblescores")
        .attr("x", d => xScale(xAccessor(d)))
        .attr("y", d => yScale(yAccessor(d)))
        .attr("width", xScale.bandwidth())
        .attr("height", yScale.bandwidth())
        .attr("fill", "#dddddd")
    


    // ----------------------
    // 6. Draw peripherals

    axisStartingNumber = Math.floor(minScoreInView / 10) * 10
    axisEndingNumber = Math.ceil(maxScoreInView / 10) * 10
    axisTickValues = d3.range(axisStartingNumber, axisEndingNumber, 10)

    const yAxisGenerator = d3.axisLeft()
        .scale(yScale)
        .tickSize(0)
        .tickValues(axisTickValues)

    const yAxis = bounds.append("g")
        .call(yAxisGenerator)

    const yAxisLabel = yAxis.append("text")
        .attr("x", -dimensions.boundedHeight / 2)
        .attr("y", -dimensions.margin.left + 10)
        .attr("fill", "black")
        .style("font-size", "1.4em")
        .text("Winning score")
        .style("text-transform", "capitalize")
        .style("transform", "rotate(-90deg)")
        .style("text-anchor", "middle")

    const xAxisGenerator = d3.axisBottom()
        .scale(xScale)
        .tickSize(0)
        .tickValues(axisTickValues)

    const xAxis = bounds.append("g")
        .call(xAxisGenerator)
            .style("transform", `translateY(${dimensions.boundedHeight}px)`)

    const xAxisLabel = xAxis.append("text")
        .attr("x", dimensions.boundedWidth / 2)
        .attr("y", dimensions.margin.bottom - 10)
        .attr("fill", "black")
        .style("font-size", "1.4em")
        .text("Losing score")
        .style("text-transform", "capitalize")

    

    const statsbox = d3.select(".statsbox")
    const statsboxboundingrect = statsbox.node().getBoundingClientRect()

    statsbox
        .style("transform", `translate(${dimensions.boundedWidth - statsboxboundingrect.width - 50}px,${dimensions.boundedHeight - statsboxboundingrect.height - 50}px)`)
    
    d3.select(".totalnumberofscorigamis")
        .text(totalnumberofscorigamis)

    d3.select(".mostrecentscorigami")
        .text(mostrecentscorigamidate)

    d3.select("#mostrecentwiningteam")
        .text(`${mostrecentscorigami.first_team_win} (${mostrecentscorigami.pts_win})`)
    
    d3.select("#mostrecentlosingteam")
        .text(`${mostrecentscorigami.first_team_lose} (${mostrecentscorigami.pts_lose})`)

    // ----------------------
    // 7. Set up interactions

    const tooltip = d3.select(".tooltip")

    function onMouseEnter(event, datum) {
        cleanupTooltip()
        cleanupBlankspaceTooltip()

        const activescorigami = bounds.append("rect")
            .attr("class", "activescorigami")
            .attr("x", d => xScale(xAccessor(datum)))
            .attr("y", d => yScale(yAccessor(datum)))
            .attr("width", xScale.bandwidth())
            .attr("height", yScale.bandwidth())
            .attr("fill", "black")
            .style("pointer-events", "none")

        tooltip.select("#tooltip-winningscore")
            .text(yAccessor(datum))
        
        tooltip.select("#tooltip-losingscore")
            .text(xAccessor(datum))

        tooltip.select("#tooltip-firstgamewinningteam")
            .text(firstgamewinningteam(datum))
        
        tooltip.select("#tooltip-firstgamelosingteam")
            .text(firstgamelosingteam(datum))

        tooltip.select("#tooltip-latestgamewinningteam")
            .text(latestgamewinningteam(datum))
        
        tooltip.select("#tooltip-latestgamelosingteam")
            .text(latestgamelosingteam(datum))

        const x = xScale(xAccessor(datum)) + dimensions.margin.left + xScale.bandwidth() / 2
        const y = yScale(yAccessor(datum)) + dimensions.margin.top
        
        tooltip.style("transform", `translate(calc( -50% + ${x}px),calc(-100% + ${y}px))`)
        tooltip.style("opacity", 1)
    }

    function onMouseLeave() {
        cleanupTooltip()
    }

    function cleanupTooltip() {
        d3.selectAll(".activescorigami")
            .remove()
        tooltip.style("opacity", 0)
    }

    const possiblescoreindicator = d3.select(".possiblescoreindicator")

    function onMouseEnterBlankspace(event, datum) {
        cleanupBlankspaceTooltip()
        cleanupTooltip()

        const activeblankspace = bounds.append("rect")
            .attr("class", "activeblankspace")
            .attr("x", d => xScale(xAccessor(datum)))
            .attr("y", d => yScale(yAccessor(datum)))
            .attr("width", xScale.bandwidth())
            .attr("height", yScale.bandwidth())
            .attr("fill", "#cccccc")
            .style("pointer-events", "none")

        possiblescoreindicator.select("#possiblescoreindicator-winningscore")
            .text(yAccessor(datum))
        
        possiblescoreindicator.select("#possiblescoreindicator-losingscore")
            .text(xAccessor(datum))

        const x = xScale(xAccessor(datum)) + dimensions.margin.left + xScale.bandwidth() / 2
        const y = yScale(yAccessor(datum)) + dimensions.margin.top
        
        possiblescoreindicator.style("transform", `translate(calc( -50% + ${x}px),calc(-100% + ${y}px))`)
        possiblescoreindicator.style("opacity", 1)
    }

    function onMouseLeaveBlankspace() {
        cleanupBlankspaceTooltip()
    }

    function cleanupBlankspaceTooltip() {
        d3.selectAll(".activeblankspace")
            .remove()
        possiblescoreindicator.style("opacity", 0)
    }

    // Clear both tooltips when you tap outside 
    d3.select("header").on("touchstart", () => {
        cleanupTooltip()
        cleanupBlankspaceTooltip()
    })

    d3.select(".controls").on("touchstart", () => {
        cleanupTooltip()
        cleanupBlankspaceTooltip()
    })

    d3.selectAll(".impossiblescores").on("touchstart", () => {
        cleanupTooltip()
        cleanupBlankspaceTooltip()
    })

    // Change metric buttons
    dateMetricButton = d3.select(".change-metric.date")
    countMetricButton = d3.select(".change-metric.count")
    dateMetricButton.node().addEventListener("click", dateMetricButtonClick)
    countMetricButton.node().addEventListener("click", countMetricButtonClick)
    
    function dateMetricButtonClick() {
        drawHeatMapRects("date")
        dateMetricButton.attr("class", "change-metric date active")
        countMetricButton.attr("class", "change-metric count")
    }

    function countMetricButtonClick() {
        drawHeatMapRects("count")
        dateMetricButton.attr("class", "change-metric date")
        countMetricButton.attr("class", "change-metric count active")
    }

}

drawScorigamiViz()