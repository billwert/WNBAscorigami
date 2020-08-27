async function drawScorigamiViz() {


    // 1. Access data

    const dataset = await d3.json("../../datafile.json")

    const yAccessor = d => d.pts_win
    const xAccessor = d => d.pts_lose


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

    const yScale = d3.scaleLinear()
        .domain([30, d3.max(dataset, yAccessor)])
        .range([dimensions.boundedHeight, 0])
    
    const xScale = d3.scaleLinear()
        .domain([30, d3.max(dataset, xAccessor)])
        .range([0, dimensions.boundedWidth])


    // 5. Draw data

    bounds.selectAll("rects")
        .data(dataset)
        .join("rect")
        .attr("x", d => xScale(xAccessor(d)))
        .attr("y", d => yScale(yAccessor(d)))
        .attr("width", 5)
        .attr("height", 5)
        .attr("fill", "#FA4D01")



}

drawScorigamiViz()