async function drawScorigamiViz() {


    // 1. Access data

    const dataset = await d3.json("../../datafile.json")

    const yAccessor = d => d.pts_win
    const xAccessor = d => d.pts_lose
    const minScoreInView = 30

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
        width: d3.select(".viz").node().getBoundingClientRect().width,
        height: d3.select(".viz").node().getBoundingClientRect().height,
        margin: {
          top: 15,
          right: 15,
          bottom: 40,
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
        .paddingInner(0.05)
    
    const xScale = d3.scaleBand()
        .domain(d3.range(minScoreInView, d3.max(dataset, xAccessor) + 1))
        .range([0, dimensions.boundedHeight]) // Using boundedHeight instead of boundedWeight for square marks and viz
        .paddingInner(0.05)


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

}

drawScorigamiViz()