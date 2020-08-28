async function drawScorigamiViz() {


    // 1. Access data

    const dataset = await d3.json("../../datafile.json")

    const yAccessor = d => d.pts_win
    const xAccessor = d => d.pts_lose

    const firstgamewinningteam = d => d.first_team_win
    const firstgamelosingteam = d => d.first_team_lose
    const latestgamewinningteam = d => d.last_team_win
    const latestgamelosingteam = d => d.last_team_lose

    const minScoreInView = 30
    const maxScoreInView = d3.max(dataset, yAccessor)

    // Create impossible scores data
    var impossiblescores = []

    for (i = minScoreInView; i <= d3.max(dataset, yAccessor); i++) {
        for (j = minScoreInView; j <= d3.max(dataset, xAccessor);j++) {
            if (i<=j){
                impossiblescores.push({"pts_win" : i, "pts_lose" : j})
            }
        }
    }


    // 2. Create chart dimensions

    let dimensions = {
        width: window.innerHeight * 0.9, // height so that it is square
        height: window.innerHeight * 0.9,
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

        
    // 4. Create scales

    const yScale = d3.scaleBand()
        .domain(d3.range(minScoreInView, d3.max(dataset, yAccessor) + 1))
        .range([dimensions.boundedHeight, 0])
        .paddingInner(0.1)
    
    const xScale = d3.scaleBand()
        .domain(d3.range(minScoreInView, d3.max(dataset, xAccessor) + 1))
        .range([0, dimensions.boundedHeight]) // Using boundedHeight instead of boundedWeight for square marks and viz
        .paddingInner(0.1)


    // 5. Draw data
    
    // 5a. Heatmap recs
    bounds.selectAll(".scorigamiscores")
        .data(dataset)
        .join("rect")
        .attr("class", "scorigamiscores")
        .attr("x", d => xScale(xAccessor(d)))
        .attr("y", d => yScale(yAccessor(d)))
        .attr("width", xScale.bandwidth())
        .attr("height", yScale.bandwidth())
        .attr("fill", "#FA4D01")
        .on("mouseenter", onMouseEnter)
        .on("mouseleave", onMouseLeave)


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


    // 7. Set up interactions

    const tooltip = d3.select(".tooltip")

    function onMouseEnter(event, datum) {
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
        d3.selectAll(".activescorigami")
            .remove()
        tooltip.style("opacity", 0)
    }
}

drawScorigamiViz()